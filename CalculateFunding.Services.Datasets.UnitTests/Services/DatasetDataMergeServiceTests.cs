using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Services.Core.Interfaces.AzureStorage;
using CalculateFunding.Services.DataImporter;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.DataImporter.Models;
using FluentAssertions;
using Moq;
using OfficeOpenXml;
using Serilog.Core;
using CalculateFunding.Models.Datasets;

namespace CalculateFunding.Services.Datasets.Services
{
    [TestClass]
    public class DatasetDataMergeServiceTests
    {
        private Mock<IBlobClient> _blobClient;
        
        private DatasetDataMergeService _service;

        [TestInitialize]
        public void SetUp()
        {
            _blobClient = new Mock<IBlobClient>();

            _service = new DatasetDataMergeService(_blobClient.Object, 
                Logger.None, 
                new ExcelDatasetReader(), 
                new DataDefinitionExcelWriter(), 
                DatasetsResilienceTestHelper.GenerateTestPolicies());
        }

        [DataTestMethod]
        [DataRow("PE_and_Sports_Grant_Data_v1.xlsx", "PE_and_Sports_Grant_Data_v2.xlsx", "PE_and_Sports_Grant_Data_v2.xlsx", "PE_and_Sports_Grant_Data_result.xlsx", "TestDatasetDefinition_PSG.json", DatasetEmptyFieldEvaluationOption.AsNull)]
        [DataRow("DSG_Rate_and_Baselines_data_V1.xlsx", "DSG_Rate_and_Baselines_data_V2.xlsx", "DSG_Rate_and_Baselines_data_V2.xlsx", "DSG_Rate_and_Baselines_data_result.xlsx", "TestDatasetDefinition_DSG.json", DatasetEmptyFieldEvaluationOption.NA)]
        [DataRow("PE_and_Sports_Grant_Data_Ignore_v1.xlsx", "PE_and_Sports_Grant_Data_Ignore_v2.xlsx", "PE_and_Sports_Grant_Data_Ignore_v2.xlsx", "PE_and_Sports_Grant_Data_Ignore_result.xlsx", "TestDatasetDefinition_PSG.json", DatasetEmptyFieldEvaluationOption.Ignore)]
        [DataRow("PE_and_Sports_Grant_Data_AsNull_v1.xlsx", "PE_and_Sports_Grant_Data_AsNull_v2.xlsx", "PE_and_Sports_Grant_Data_AsNull_v2.xlsx", "PE_and_Sports_Grant_Data_AsNull_result.xlsx", "TestDatasetDefinition_PSG.json", DatasetEmptyFieldEvaluationOption.AsNull)]
        public async Task Merge_ShouldGetTheNewAndUpdatedData(
            string latestBlobFileName, 
            string blobFileNameToMerge, 
            string blobFileNameFinal, 
            string resultsFile, 
            string definitionFileName,
            DatasetEmptyFieldEvaluationOption datasetEmptyFieldEvaluationOption)
        {
            DatasetDefinition datasetDefinition = GetDatasetDefinitionByName(definitionFileName);

            await using Stream latestDatasetStream = File.OpenRead($"TestItems{Path.DirectorySeparatorChar}{latestBlobFileName}");

            BlobClient latestFileBlob = new BlobClient(new Uri($"http://localhost/{latestBlobFileName}"));

            _blobClient.Setup(_ => _.GetBlobReferenceFromServerAsync(latestBlobFileName))
                .ReturnsAsync(latestFileBlob);

            await using Stream fileToMergeDatasetStream = File.OpenRead($"TestItems{Path.DirectorySeparatorChar}{blobFileNameToMerge}");

            BlobClient fileToMergeBlob = new BlobClient(new Uri($"http://localhost/{blobFileNameToMerge}"));
            _blobClient.Setup(_ => _.GetBlobReferenceFromServerAsync(blobFileNameToMerge))
                .ReturnsAsync(fileToMergeBlob);

            _blobClient.SetupSequence(_ => _.DownloadToStreamAsync(It.IsAny<BlobBaseClient>()))
                .ReturnsAsync(latestDatasetStream)
                .ReturnsAsync(fileToMergeDatasetStream);

            await using MemoryStream uploadedStream = new MemoryStream();

            // capture upload via IBlobClient.UploadAsync since production code uses that via the blob client
            _blobClient.Setup(_ => _.UploadAsync(It.IsAny<BlobClient>(), It.IsAny<Stream>()))
                .Callback<BlobClient, Stream>((b, s) =>
                {
                    s.Position = 0;
                    s.CopyTo(uploadedStream);
                })
                .Returns(Task.CompletedTask);

            DatasetDataMergeResult result = await _service.Merge(datasetDefinition, latestBlobFileName, blobFileNameFinal, datasetEmptyFieldEvaluationOption);

            result.TablesMergeResults.Count().Should().Be(1);

            await using Stream expectedResultStream = File.OpenRead($"TestItems{Path.DirectorySeparatorChar}{resultsFile}");

            using ExcelPackage expected = new ExcelPackage(expectedResultStream);
            uploadedStream.Position = 0;
            using ExcelPackage actual = new ExcelPackage(uploadedStream);

            ExcelWorksheet expectedWorksheet = expected.Workbook.Worksheets[1];
            ExcelWorksheet actualWorksheet = actual.Workbook.Worksheets[1];

            // Compare row counts
            actualWorksheet.Dimension.Rows.Should().Be(expectedWorksheet.Dimension.Rows, "Row count should match");
            actualWorksheet.Dimension.Columns.Should().Be(expectedWorksheet.Dimension.Columns, "Column count should match");

            // Compare headers (row 1)
            for (int j = 1; j <= expectedWorksheet.Dimension.Columns; j++)
            {
                string actualHeader = actualWorksheet.Cells[1, j].Value?.ToString();
                string expectedHeader = expectedWorksheet.Cells[1, j].Value?.ToString();
                actualHeader.Should().Be(expectedHeader, $"Header[{j}] mismatch");
            }

            // Build dictionaries of rows keyed by identifier (column 1) for content comparison
            // The merge service appends new rows at the end, so we compare by identifier not position
            var expectedRows = new Dictionary<string, List<string>>();
            var actualRows = new Dictionary<string, List<string>>();

            for (int i = 2; i <= expectedWorksheet.Dimension.Rows; i++)
            {
                string identifier = expectedWorksheet.Cells[i, 1].Value?.ToString();
                var rowValues = new List<string>();
                for (int j = 1; j <= expectedWorksheet.Dimension.Columns; j++)
                {
                    rowValues.Add(expectedWorksheet.Cells[i, j].Value?.ToString());
                }
                expectedRows[identifier] = rowValues;
            }

            for (int i = 2; i <= actualWorksheet.Dimension.Rows; i++)
            {
                string identifier = actualWorksheet.Cells[i, 1].Value?.ToString();
                var rowValues = new List<string>();
                for (int j = 1; j <= actualWorksheet.Dimension.Columns; j++)
                {
                    rowValues.Add(actualWorksheet.Cells[i, j].Value?.ToString());
                }
                actualRows[identifier] = rowValues;
            }

            // Compare each expected row with actual row by identifier
            foreach (var expectedRow in expectedRows)
            {
                actualRows.Should().ContainKey(expectedRow.Key, $"Identifier {expectedRow.Key} should exist in actual");
                var actualRowValues = actualRows[expectedRow.Key];
                for (int j = 0; j < expectedRow.Value.Count; j++)
                {
                    string actualVal = actualRowValues[j];
                    string expectedVal = expectedRow.Value[j];
                    
                    // Normalize comparison for numeric values - treat null as equivalent to "0" or ""
                    // This handles cases where empty cells in Excel can be read as null or as 0 depending on the field type
                    bool isEquivalent = actualVal == expectedVal ||
                        (string.IsNullOrEmpty(actualVal) && (expectedVal == "0" || string.IsNullOrEmpty(expectedVal))) ||
                        (string.IsNullOrEmpty(expectedVal) && (actualVal == "0" || string.IsNullOrEmpty(actualVal)));
                    
                    isEquivalent.Should().BeTrue(
                        $"Row with identifier {expectedRow.Key}, Column[{j + 1}] mismatch: actual='{actualVal ?? "null"}', expected='{expectedVal ?? "null"}'");
                }
            }

            // Verify no extra rows in actual
            actualRows.Count.Should().Be(expectedRows.Count, "Number of data rows should match");
        }

        private static DatasetDefinition GetDatasetDefinitionByName(string datasetDefinitionName)
            => File.ReadAllText($"DatasetDefinitions{Path.DirectorySeparatorChar}{datasetDefinitionName}")
                .AsPoco<DatasetDefinition>();
    }
}
