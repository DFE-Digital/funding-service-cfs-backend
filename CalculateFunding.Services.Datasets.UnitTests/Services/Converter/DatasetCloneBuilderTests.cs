using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CalculateFunding.Common.Models;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Datasets.Converter;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Services.Core;
using CalculateFunding.Services.Core.Interfaces;
using CalculateFunding.Services.Core.Interfaces.AzureStorage;
using CalculateFunding.Services.DataImporter;
using CalculateFunding.Services.Datasets.Converter;
using CalculateFunding.Services.Datasets.Interfaces;
using CalculateFunding.Tests.Common.Helpers;
using FluentAssertions;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Polly;

namespace CalculateFunding.Services.Datasets.Services.Converter
{
    [TestClass]
    public class DatasetCloneBuilderTests
    {
        private Mock<IBlobClient> _blobs;
        private Mock<IExcelDatasetReader> _reader;
        private Mock<IExcelDatasetWriter> _writer;
        private Mock<IDatasetIndexer> _indexer;
        private Mock<IDatasetRepository> _datasets;
        private Mock<IVersionRepository<DatasetVersion>> _datasetsVersionRepository;

        private byte[] _lastUploadedBlobData;
        private IDictionary<string, string> _lastMetadata;

        private DatasetCloneBuilder _builder;

        [TestInitialize]
        public void SetUp()
        {
            _blobs = new Mock<IBlobClient>();
            _reader = new Mock<IExcelDatasetReader>();
            _indexer = new Mock<IDatasetIndexer>();
            _writer = new Mock<IExcelDatasetWriter>();
            _datasets = new Mock<IDatasetRepository>();
            _datasetsVersionRepository = new Mock<IVersionRepository<DatasetVersion>>();

            _builder = new DatasetCloneBuilder(_blobs.Object,
                _datasets.Object,
                _datasetsVersionRepository.Object,
                _reader.Object,
                _writer.Object,
                _indexer.Object,
                new DatasetsResiliencePolicies
                {
                    BlobClient = Policy.NoOpAsync(),
                    DatasetRepository = Policy.NoOpAsync()
                });
        }

        [TestMethod]
        public void LoadOriginalDatasetFailsWhenNoDatasetSupplied()
        {
            Func<Task> invocation = () => WhenTheOriginalDatasetIsLoaded(null, NewDatasetDefinition());

            invocation
                .Should()
                .ThrowAsync<NonRetriableException>()
                .Result
                .Which
                .Message
                .Should()
                .Be("No dataset supplied to load excel blob data from.");
        }

        [TestMethod]
        public void LoadOriginalDatasetFailsWhenNoDatasetDefinitionSupplied()
        {
            Func<Task> invocation = () => WhenTheOriginalDatasetIsLoaded(NewDataset(), null);

            invocation
                .Should()
                .ThrowAsync<NonRetriableException>()
                .Result
                .Which
                .Message
                .Should()
                .Be("No dataset definition supplied to load excel blob data from.");
        }

        [TestMethod]
        public void LoadOriginalDatasetFailsWhenNoLocatedForSuppliedDatasetDetails()
        {
            Func<Task> invocation = () => WhenTheOriginalDatasetIsLoaded(NewDataset(), NewDatasetDefinition());

            invocation
                .Should()
                .ThrowAsync<NonRetriableException>()
                .Result
                .Which
                .Message
                .Should()
                .StartWith("No blob located with path ");
        }

        [TestMethod]
        public void LoadOriginalDatasetFailsWhenExcelStreamForSuppliedDatasetDetailsHasNoData()
        {
            Dataset dataset = NewDataset();
            DatasetDefinition datasetDefinition = NewDatasetDefinition();

            BlobClient blob = NewBlob();

            GivenTheBlob(dataset.Current.BlobName, blob);
            AndTheBlobStream(new MemoryStream());

            Func<Task> invocation = () => WhenTheOriginalDatasetIsLoaded(dataset, datasetDefinition);

            invocation
                .Should()
                .ThrowAsync<NonRetriableException>()
                .Result
                .Which
                .Message
                .Should()
                .EndWith(" contains no data.");
        }

        [TestMethod]
        public async Task LoadOriginalDatasetReadsExcelDataFromBlobIndicatedFromSuppliedDatasetDetails()
        {
            Dataset dataset = NewDataset();
            DatasetDefinition datasetDefinition = NewDatasetDefinition();

            BlobClient blob = NewBlob();

            GivenTheBlob(dataset.Current.BlobName, blob);

            MemoryStream excelStream = new MemoryStream(new byte[10]);

            AndTheBlobStream(excelStream);

            IEnumerable<TableLoadResult> expectedData = new[]
            {
                NewTableLoadResult(),
                NewTableLoadResult(),
                NewTableLoadResult(),
                NewTableLoadResult()
            };

            AndTheTableLoadResults(excelStream, datasetDefinition, expectedData);

            await WhenTheOriginalDatasetIsLoaded(dataset, datasetDefinition);

            _builder
                .DatasetData
                .Should()
                .BeEquivalentTo(expectedData);
        }

        [TestMethod]
        public void GetExistingIdentifierValuesQueriesTheDatasetForTheSuppliedFieldNameValue()
        {
            string fieldName = NewRandomString();

            string valueOne = NewRandomString();
            string valueTwo = NewRandomString();
            string valueThree = NewRandomString();

            TableLoadResult datasetTable = NewTableLoadResult(_ =>
                _.WithRows(NewRowLoadResult(row => row.WithFields(NewField(),
                        NewField(fieldName, valueOne),
                        NewField())),
                    NewRowLoadResult(row => row.WithFields(NewField(),
                        NewField(fieldName, valueTwo),
                        NewField())),
                    NewRowLoadResult(row => row.WithFields(NewField(),
                        NewField(fieldName, valueThree),
                        NewField()))));

            GivenTheDatasetData(datasetTable);

            IEnumerable<string> existingIdentifierValues = WhenTheExistingIdentifierValuesAreQueried(fieldName);

            existingIdentifierValues
                .Should()
                .BeEquivalentTo(valueOne, valueTwo, valueThree);
        }

        [TestMethod]
        public void CopyRowReturnsNotFoundResultIfNoMatchForSourceIdInDataset()
        {
            TableLoadResult datasetTable = NewTableLoadResult(_ => _.WithRows(NewRowLoadResult()));

            GivenTheDatasetData(datasetTable);

            string fieldName = NewRandomString();
            string sourceProviderId = NewRandomString();
            string destinationProviderId = NewRandomString();

            RowCopyResult result = WhenTheRowIsCopied(fieldName, sourceProviderId, destinationProviderId);

            result
                .Should()
                .BeEquivalentTo(
                    NewRowCopyResult(r => r
                        .WithOutcome(RowCopyOutcome.SourceRowNotFound)
                        .WithEligibleConverter(
                            NewEligibleConverter(ec => ec
                                .WithPreviousProviderIdentifier(sourceProviderId)
                                .WithTargetProviderId(destinationProviderId)
                            )
                        )
                    )
                );

            datasetTable.Rows
                .Count
                 .Should()
                 .Be(1);
        }
        
        [TestMethod]
        public void CopyRowReturnsDestinationExistsResultIfMatchForDestinationIdInTheDataset()
        {
            string fieldName = NewRandomString();
            string sourceProviderId = NewRandomString();
            string destinationProviderId = NewRandomString();

            
            TableLoadResult datasetTable = NewTableLoadResult(_ => _.WithRows(
                NewRowLoadResult(row => row.WithFields(NewField(fieldName, sourceProviderId))),
                    NewRowLoadResult(row => row.WithFields(NewField(fieldName, destinationProviderId)))));

            GivenTheDatasetData(datasetTable);

            
            RowCopyResult result = WhenTheRowIsCopied(fieldName, sourceProviderId, destinationProviderId);

            result
                .Should()
                .BeEquivalentTo(
                    NewRowCopyResult(r => r
                        .WithOutcome(RowCopyOutcome.DestinationRowAlreadyExists)
                        .WithEligibleConverter(
                            NewEligibleConverter(ec => ec
                                .WithPreviousProviderIdentifier(sourceProviderId)
                                .WithTargetProviderId(destinationProviderId)
                            )
                        )
                    )
                );
            
            datasetTable.Rows
                .Count
                .Should()
                .Be(2);
        }
        
        [TestMethod]
        public void CopyRowCopiesSourceRowToDestinationIdentifierIdAndAddsToDatasetIfNotInDatasetYet()
        {
            string fieldName = NewRandomString();
            int sourceProviderId = NewRandomUKPRN();
            string destinationProviderId = NewRandomString();

            (string, object) fieldOne = NewField();
            (string, object) fieldTwo = NewField();

            RowLoadResult sourceRow = NewRowLoadResult(row => row.WithFields(NewField(fieldName, sourceProviderId), 
                fieldOne, 
                fieldTwo));
            
            TableLoadResult datasetTable = NewTableLoadResult(_ => _.WithRows(
                sourceRow));

            GivenTheDatasetData(datasetTable);
            
            RowCopyResult result = WhenTheRowIsCopied(fieldName, sourceProviderId.ToString(), destinationProviderId);

            result
                .Should()
                .BeEquivalentTo(
                    NewRowCopyResult(r => r
                        .WithOutcome(RowCopyOutcome.Copied)
                        .WithEligibleConverter(
                            NewEligibleConverter(ec => ec
                                .WithPreviousProviderIdentifier(sourceProviderId.ToString())
                                .WithTargetProviderId(destinationProviderId)
                            )
                        )
                    )
                );
            
            datasetTable.Rows
                .Count
                .Should()
                .Be(2);
            
            datasetTable.Rows
                .Last()
                .Should()
                .BeEquivalentTo(NewRowLoadResult(row => row
                    .WithIdentifierFieldType(IdentifierFieldType.UKPRN)
                    .WithIdentifier(destinationProviderId)
                    .WithFields(NewField(fieldName, destinationProviderId), 
                    fieldOne, 
                    fieldTwo)));
        }
        
        [TestMethod]
        public async Task SaveContentsCreatesAndIndexesANewDatasetVersionAndUploadsANewXlsBlob()
        {
            Reference author = NewReference();
            string providerVersionId = NewRandomString();
            Dataset dataset = NewDataset();
            List<DatasetVersion> history = new List<DatasetVersion> { NewDatasetVersion(ver => ver.WithVersion(97)),
                NewDatasetVersion(ver => ver.WithVersion(98)) };
            DatasetVersion currentVersion = dataset.Current;
            currentVersion.Version = 99;
            currentVersion.ChangeType = DatasetChangeType.ConverterWizard;
            DatasetDefinition datasetDefinition = NewDatasetDefinition();
            
            TableLoadResult datasetTable = NewTableLoadResult(_ => _.WithRows(NewRowLoadResult(),
                NewRowLoadResult(),
                NewRowLoadResult(),
                NewRowLoadResult()));

            byte[] expectedExcelData = NewRandomString().AsUTF8Bytes();
            
            GivenTheDatasetData(datasetTable);
            AndTheDatasetSavesSuccessfully(dataset);
            AndTheExcelDataForTheDatasetData(datasetDefinition, datasetTable, expectedExcelData);
            DatasetVersion clonedDatasetVersion = AndTheDatasetVersionCreated(currentVersion, dataset.Id, author, providerVersionId, datasetTable.Rows.Count, currentVersion.Version);
            AndTheDatasetVersionSaved(clonedDatasetVersion);

            BlobClient blob = NewBlob();

            string currentBlobName = dataset.Current.BlobName;

            BlobClient blobClient = NewBlob();

            AndTheBlobReference($"{dataset.Id}/v100/{currentBlobName}", blobClient);

            // capture upload and metadata via IBlobClient mock
            _blobs.Setup(_ => _.UploadAsync(It.IsAny<BlobClient>(), It.IsAny<Stream>()))
                .Callback<BlobClient, Stream>((b, s) => { using (var ms = new MemoryStream()) { s.Position = 0; s.CopyTo(ms); _lastUploadedBlobData = ms.ToArray(); } })
                .Returns(Task.CompletedTask);

            _blobs.Setup(_ => _.AddMetadataAsync(It.IsAny<BlobClient>(), It.IsAny<IDictionary<string, string>>()))
                .Callback<BlobClient, IDictionary<string, string>>((b, m) => { _lastMetadata = m; })
                .Returns(Task.CompletedTask);

            await WhenTheContentsAreSaved(author, datasetDefinition, dataset, providerVersionId);

            DatasetVersion newVersion = dataset.Current;
            DatasetVersion expectedNewVersion = (DatasetVersion) currentVersion.Clone();
            expectedNewVersion.Author = author;
            expectedNewVersion.RowCount = datasetTable.Rows.Count;
            expectedNewVersion.Version++;
            expectedNewVersion.ChangeType = DatasetChangeType.ConverterWizard;
            expectedNewVersion.BlobName = $"{dataset.Id}/v100/{currentBlobName}";
            expectedNewVersion.ProviderVersionId = providerVersionId;

            newVersion
                .Should()
                .BeEquivalentTo(expectedNewVersion);

            _indexer.Verify(_ => _.IndexDatasetAndVersion(dataset), 
                Times.Once);

            _lastUploadedBlobData
                .Should()
                .BeEquivalentTo(expectedExcelData);
            
            AndTheBlobHasMetadataMatchingTheDataset(dataset, datasetDefinition);

            _blobs.Verify(_ => _.AddMetadataAsync(It.IsAny<BlobClient>(), It.IsAny<IDictionary<string, string>>()), Times.Once());
        }

        private void AndTheBlobHasMetadataMatchingTheDataset(Dataset dataset,
            DatasetDefinition datasetDefinition)
        {
            AssertThatDictionaryContainsEntry(_lastMetadata, ("datadefinitionid", datasetDefinition.Id));
            AssertThatDictionaryContainsEntry(_lastMetadata, ("datasetid", dataset.Id));
            AssertThatDictionaryContainsEntry(_lastMetadata, ("authorid", dataset.Current.Author.Id));
            AssertThatDictionaryContainsEntry(_lastMetadata, ("authorname", dataset.Current.Author.Name));
            AssertThatDictionaryContainsEntry(_lastMetadata, ("name", dataset.Current.BlobName));
            AssertThatDictionaryContainsEntry(_lastMetadata, ("description", dataset.Current.Description));
            AssertThatDictionaryContainsEntry(_lastMetadata, ("fundingstreamid", datasetDefinition.FundingStreamId));
            AssertThatDictionaryContainsEntry(_lastMetadata, ("converterwizard", true.ToString().ToLower()));
        }

        private void AssertThatDictionaryContainsEntry(IDictionary<string, string> dictionary,
            (string key, string value) expectedValue)
        {
            dictionary
                .ContainsKey(expectedValue.key)
                .Should()
                .BeTrue();

            dictionary[expectedValue.key]
                .Should()
                .BeEquivalentTo(expectedValue.value);
        }
        

        private async Task WhenTheContentsAreSaved(Reference author,
            DatasetDefinition datasetDefinition,
            Dataset dataset,
            string providerVersionId)
            => await _builder.SaveContents(author, providerVersionId, datasetDefinition, dataset);

        private RowCopyResult WhenTheRowIsCopied(string fieldName,
            string sourceProviderId,
            string destinationProviderId) =>
            _builder.CopyRow(fieldName, sourceProviderId, destinationProviderId);

        private void AndTheBlobReference(string path,
            BlobClient blob)
            => _blobs.Setup(_ => _.GetBlobReferenceFromServerAsync(path))
                .ReturnsAsync(blob);

        private void AndTheDatasetSavesSuccessfully(Dataset dataset)
            => _datasets.Setup(_ => _.SaveDataset(dataset))
                .ReturnsAsync(HttpStatusCode.OK);

        private void AndTheExcelDataForTheDatasetData(DatasetDefinition datasetDefinition,
            TableLoadResult datasetData,
            byte[] excelData)
        {
            _writer.Setup(w => w.Write(
                It.Is<DatasetDefinition>(d => d == datasetDefinition),
                It.Is<IEnumerable<TableLoadResult>>(tables => tables.SequenceEqual(new[] { datasetData }))
            )).Returns(excelData);
        }

        private DatasetVersion AndTheDatasetVersionCreated(DatasetVersion datasetVersion, string datasetId, Reference author, string providerVersion, int rowCount, int version = 1)
        {
            DatasetVersion clonedDatasetVersion = (DatasetVersion)datasetVersion.Clone();
            clonedDatasetVersion.Author = author;
            clonedDatasetVersion.ProviderVersionId = providerVersion;
            clonedDatasetVersion.RowCount = rowCount;
            clonedDatasetVersion.Version = version + 1;

            _datasetsVersionRepository
                .Setup(_ => _.CreateVersion(It.Is<DatasetVersion>(cv => cv.BlobName == clonedDatasetVersion.BlobName), It.Is<DatasetVersion>(cv => cv.Id == datasetVersion.Id), null, false))
                .ReturnsAsync(clonedDatasetVersion);

            return clonedDatasetVersion;
        }

        private void AndTheDatasetVersionSaved(DatasetVersion datasetVersion)
        {
            _datasetsVersionRepository
                .Setup(_ => _.SaveVersion(It.Is<DatasetVersion>(dv => dv.Id == datasetVersion.Id)))
                .ReturnsAsync(HttpStatusCode.OK);
        }

        private IEnumerable<string> WhenTheExistingIdentifierValuesAreQueried(string identifierFieldName)
        {
            return _builder.GetExistingIdentifierValues(identifierFieldName);
        }

        private void GivenTheDatasetData(params TableLoadResult[] tables)
        {
            _builder.DatasetData = tables;
        }

        private async Task WhenTheOriginalDatasetIsLoaded(Dataset dataset,
            DatasetDefinition datasetDefinition)
        {
            await _builder.LoadOriginalDataset(dataset, datasetDefinition);
        }

        private void GivenTheBlob(string path,
            BlobClient blob)
        {
            _blobs.Setup(_ => _.GetBlobReferenceFromServerAsync(path))
                .ReturnsAsync(blob);
        }

        private void AndTheBlob(string path,
            BlobClient blob)
            => _blobs.Setup(_ => _.GetBlockBlobReference(path))
                .Returns(new BlockBlobClient(new Uri(blob.Uri.ToString())));

        private void AndTheBlobStream(Stream stream)
        {
            _blobs.Setup(_ => _.DownloadToStreamAsync(It.IsAny<BlobBaseClient>()))
                .ReturnsAsync(stream);
        }

        private void AndTheTableLoadResults(Stream excelStream,
            DatasetDefinition datasetDefinition,
            IEnumerable<TableLoadResult> results)
        {
            _reader.Setup(_ => _.Read(excelStream, datasetDefinition))
                .Returns(results);
        }

        private BlobClient NewBlob()
        {
            return new BlobClient(new Uri($"http://localhost/{Guid.NewGuid()}"));
        }

        private Reference NewReference() => new ReferenceBuilder().Build();

        private RowCopyResult NewRowCopyResult(Action<RowCopyResultBuilder> setUp = null)
        {
            RowCopyResultBuilder rowCopyResultBuilder = new RowCopyResultBuilder();

            setUp?.Invoke(rowCopyResultBuilder);

            return rowCopyResultBuilder.Build();
        }

        private RowLoadResult NewRowLoadResult(Action<RowLoadResultBuilder> setUp = null)
        {
            RowLoadResultBuilder rowLoadResultBuilder = new RowLoadResultBuilder();

            setUp?.Invoke(rowLoadResultBuilder);

            return rowLoadResultBuilder.Build();
        }

        private (string fieldName, object value) NewField(string fieldName = null,
            object value = null)
        {
            return (fieldName ?? NewRandomString(), value ?? NewRandomString());
        }

        private DatasetDefinition NewDatasetDefinition(Action<DatasetDefinitionBuilder> setUp = null)
        {
            DatasetDefinitionBuilder datasetDefinitionBuilder = new DatasetDefinitionBuilder();

            setUp?.Invoke(datasetDefinitionBuilder);

            return datasetDefinitionBuilder.Build();
        }

        private Dataset NewDataset(Action<DatasetBuilder> setUp = null)
        {
            DatasetVersion current = NewDatasetVersion();

            DatasetBuilder datasetBuilder = new DatasetBuilder()
                .WithCurrent(current);

            setUp?.Invoke(datasetBuilder);

            return datasetBuilder.Build();
        }

        private DatasetVersion NewDatasetVersion(Action<DatasetVersionBuilder> setUp = null)
        {
            DatasetVersionBuilder datasetVersionBuilder = new DatasetVersionBuilder();

            setUp?.Invoke(datasetVersionBuilder);

            return datasetVersionBuilder.Build();
        }

        private TableLoadResult NewTableLoadResult(Action<TableLoadResultBuilder> setUp = null)
        {
            TableLoadResultBuilder tableLoadResultBuilder = new TableLoadResultBuilder();

            setUp?.Invoke(tableLoadResultBuilder);

            return tableLoadResultBuilder.Build();
        }

        private ProviderConverter NewEligibleConverter(Action<ProviderConverterBuilder> setUp = null)
        {
            ProviderConverterBuilder eligibleConverterBuilder = new ProviderConverterBuilder();

            setUp?.Invoke(eligibleConverterBuilder);

            return eligibleConverterBuilder.Build();
        }

        private string NewRandomString()
        {
            return new RandomString();
        }

        private int NewRandomUKPRN()
        {
            return new RandomNumberBetween(1000000, 1010000);
        }
    }
}