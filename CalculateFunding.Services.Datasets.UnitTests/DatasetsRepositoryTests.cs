using CalculateFunding.Common.ApiClient.DataSets.Models;
using CalculateFunding.Common.EfCore.GenericRepository;
using CalculateFunding.Common.EfCore.UnitOfWork;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Model = CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CalculateFunding.Common.Models;
using System.Net;
using CalculateFunding.Models.Versioning;

namespace CalculateFunding.Services.Datasets.UnitTests
{

    [TestClass]
    public class DatasetsRepositoryTests
    {

        protected Mock<IUnitOfWork> _uowMock;
        protected CfsDbContext _dbContext;
        protected DataSetsRepository _datasetsRepository;

        public DatasetsRepositoryTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _dbContext = new CfsDbContext(new DbContextOptionsBuilder<CfsDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);
            _datasetsRepository = new DataSetsRepository(_uowMock.Object);
        }

        #region DatasetDefinition Methods

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetDatasetDefinition_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _datasetsRepository.GetDatasetDefinition(null);
        }

        [TestMethod]
        public async Task GetDatasetDefinition_WithInValidData_ReturnsNull()
        {
            SetUpDatasetDefintionTableData();

            var datasetDefinition = await _datasetsRepository.GetDatasetDefinition("1780709");

            Assert.IsNull(datasetDefinition);
        }

        [TestMethod]
        public async Task GetDatasetDefinition_WithValidData_ReturnsDatasetDefinition()
        {
            SetUpDatasetDefintionTableData();

            var datasetDefinition = await _datasetsRepository.GetDatasetDefinition("1780700");

            Assert.IsNotNull(datasetDefinition);
            Assert.IsInstanceOfType(datasetDefinition, typeof(Models.Datasets.Schema.DatasetDefinition));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetDatasetDefinitionsByFundingStreamId_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _datasetsRepository.GetDatasetDefinitionsByFundingStreamId(null);
        }

        [TestMethod]
        public async Task GetDatasetDefinitionsByFundingStreamId_WithInValidData_ReturnsNull()
        {
            SetUpDatasetDefintionTableData();

            var datasetDefinitions = await _datasetsRepository.GetDatasetDefinitionsByFundingStreamId("GAG");

            Assert.IsTrue(datasetDefinitions.IsNullOrEmpty());
        }

        [TestMethod]
        public async Task GetDatasetDefinitionsByFundingStreamId_WithValidData_ReturnsDatasetDefinitions()
        {
            SetUpDatasetDefintionTableData();

            var datasetDefinitions = await _datasetsRepository.GetDatasetDefinitionsByFundingStreamId("1619");

            Assert.IsTrue(!datasetDefinitions.IsNullOrEmpty());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task DatasetExistsWithGivenName_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _datasetsRepository.DatasetExistsWithGivenName(null, null);
        }

        [TestMethod]
        public async Task DatasetExistsWithGivenName_WithInValidData_ReturnsFalse()
        {
            SetUpDatasetDefintionTableData();

            Boolean isDatasetExists  = await _datasetsRepository.DatasetExistsWithGivenName("16-19 Case", "1780709");

            Assert.IsFalse(isDatasetExists);
        }

        [TestMethod]
        public async Task DatasetExistsWithGivenName_WithValidData_ReturnsTrue()
        {
            SetUpDatasetDefintionTableData();

            Boolean isDatasetExists = await _datasetsRepository.DatasetExistsWithGivenName("16-19 Test Case", "1780709");

            Assert.IsTrue(isDatasetExists);
        }

        [TestMethod]
        public async Task GetDatasetDefinitions_WithNoData_ReturnsEmpty()
        {
            SetUpDatasetDefinitionTable();

            var datasetDefinitions = await _datasetsRepository.GetDatasetDefinitions();

            Assert.IsTrue(datasetDefinitions.IsNullOrEmpty());
        }


        [TestMethod]
        public async Task GetDatasetDefinitions_WithValidData_ReturnsDatasetDefinitions()
        {
            SetUpDatasetDefintionTableData();

            var datasetDefinitions = await _datasetsRepository.GetDatasetDefinitions();

            Assert.IsTrue(!datasetDefinitions.IsNullOrEmpty());
        }

        [TestMethod]
        public async Task GetDatasetDefinitionsBySQLQuery_WithNoData_ReturnsEmpty()
        {
            SetUpDatasetDefinitionTable();

            var datasetDefinitions = await _datasetsRepository.GetDatasetDefinitionsBySQLQuery(s => s.Id == "1780709");

            Assert.IsTrue(datasetDefinitions.IsNullOrEmpty());
        }

        [TestMethod]
        public async Task GetDatasetDefinitionsBySQLQuery_WithValidData_ReturnsDatasetDefinitions()
        {
            SetUpDatasetDefintionTableData();

            var datasetDefinitions = await _datasetsRepository.GetDatasetDefinitionsBySQLQuery(s => s.Id == "1780700");

            Assert.IsTrue(!datasetDefinitions.IsNullOrEmpty());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetDatasetDefinitionDocumentByDatasetDefinitionId_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _datasetsRepository.GetDatasetDefinitionDocumentByDatasetDefinitionId(null);
        }

        [TestMethod]
        public async Task GetDatasetDefinitionDocumentByDatasetDefinitionId_WithInValidData_ReturnsNull()
        {
            SetUpDatasetDefintionTableData();

            var datasetDefinitionDocument = await _datasetsRepository.GetDatasetDefinitionDocumentByDatasetDefinitionId("1780709");

            Assert.IsNull(datasetDefinitionDocument);
        }

        [TestMethod]
        public async Task GetDatasetDefinitionDocumentByDatasetDefinitionId_WithValidData_ReturnsDatasetDefinition()
        {
            SetUpDatasetDefintionTableData();

            var datasetDefinitionDocument = await _datasetsRepository.GetDatasetDefinitionDocumentByDatasetDefinitionId("1780700");

            Assert.IsNotNull(datasetDefinitionDocument);
            Assert.IsInstanceOfType(datasetDefinitionDocument, typeof(DocumentEntity<Models.Datasets.Schema.DatasetDefinition>));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task SaveDefinition_WithNullData_ThrowArgumentException()
        {

            await _datasetsRepository.SaveDefinition(null);

        }

        [TestMethod]
        public async Task SaveDefinition_WithValidData()
        {
            SetUpDatasetDefinitionTable();
            SetUpFundingStreamTable();
            SetUpFundingStreamTableData();

            await _dbContext.SaveChangesAsync();

            Model.FieldDefinition fieldDefinition = new Model.FieldDefinition()
            {
                Id = "1780702",
                Name = "Field1",
                IdentifierFieldType = Model.IdentifierFieldType.UKPRN,
                Type = Model.FieldType.Integer
            };

            Model.TableDefinition tableDefinition = new Model.TableDefinition()
            {
                Id = "1780701",
                Name = "16-19 Test Case",
                Description = "Description",
                FieldDefinitions = new List<Model.FieldDefinition>() { fieldDefinition }
            };


            Model.DatasetDefinition datasetDefinition = new Model.DatasetDefinition()
            {
                Id = "1780700",
                Name = "16-19 Test Case",
                Version = 1,
                Description = "This dataset covers test case data for 16-19 providers",
                FundingStreamId = "1619",
                ConverterEligible = false,
                ValidateProviders = false,
                ValidateProvidersByYearRange = 1,
                TableDefinitions = new List<Model.TableDefinition>() { tableDefinition }
            };

            HttpStatusCode result = await _datasetsRepository.SaveDefinition(datasetDefinition);

            Assert.AreEqual(HttpStatusCode.Created, result);
        }


        #endregion

        #region Dataset Methods

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetDatasetByDatasetId_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _datasetsRepository.GetDatasetByDatasetId(null);
        }

        [TestMethod]
        public async Task GetDatasetByDatasetId_WithInValidData_ReturnsNull()
        {
            SetUpDatasetTableData();

            var dataset= await _datasetsRepository.GetDatasetByDatasetId("DS2");

            Assert.IsNull(dataset);
        }

        [TestMethod]
        public async Task GetDatasetByDatasetId_WithValidData_ReturnsDataset()
        {
            SetUpDatasetTableData();

            var dataset = await _datasetsRepository.GetDatasetByDatasetId("DS1");

            Assert.IsNotNull(dataset);
            Assert.IsInstanceOfType(dataset, typeof(Models.Datasets.Dataset));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetDatasetDocumentByDatasetId_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _datasetsRepository.GetDatasetDocumentByDatasetId(null);
        }

        [TestMethod]
        public async Task GetDatasetDocumentByDatasetId_WithInValidData_ReturnsNull()
        {
            SetUpDatasetTableData();

            var datasetDocument = await _datasetsRepository.GetDatasetDocumentByDatasetId("DS2");

            Assert.IsNull(datasetDocument);
        }

        [TestMethod]
        public async Task GetDatasetDocumentByDatasetId_WithValidData_ReturnsDataset()
        {
            SetUpDatasetTableData();

            var datasetDocument = await _datasetsRepository.GetDatasetDocumentByDatasetId("DS1");

            Assert.IsNotNull(datasetDocument);
            Assert.IsInstanceOfType(datasetDocument, typeof(DocumentEntity<Models.Datasets.Dataset>));
        }

        [TestMethod]
        public async Task GetDatasets_WithNoData_ReturnsEmpty()
        {
            SetUpDatasetTables();

            var datasets = await _datasetsRepository.GetDatasets();

            Assert.IsTrue(datasets.IsNullOrEmpty());
        }

        [TestMethod]
        public async Task GetDatasets_WithValidData_ReturnsDatasets()
        {
            SetUpDatasetTableData();

            var datasets = await _datasetsRepository.GetDatasets();

            Assert.IsTrue(!datasets.IsNullOrEmpty());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetDatasetLatestVersions_WithNull_ReturnsEmpty()
        {
            SetUpDatasetTableData();

            var datasetLatestVersions = await _datasetsRepository.GetDatasetLatestVersions(new List<string>());

            Assert.IsTrue(datasetLatestVersions.IsNullOrEmpty());
        }

        [TestMethod]
        public async Task GetDatasetLatestVersions_WithValidData_ReturnsDatasets()
        {
            SetUpDatasetTableData();

            var datasetLatestVersions = await _datasetsRepository.GetDatasetLatestVersions(new List<string>() {"DS1"});

            Assert.IsTrue(!datasetLatestVersions.IsNullOrEmpty());
            Assert.AreEqual(datasetLatestVersions.Where(_ => _.Key.Equals("DS1")).FirstOrDefault().Value, 1);
        }

        [TestMethod]
        public async Task GetDatasetsBySQLQuery_WithInValidData_ReturnsNull()
        {
            SetUpDatasetTableData();

            var datasets = await _datasetsRepository.GetDatasetsBySQLQuery(_ => _.DatasetId == "DS12");

            Assert.IsTrue(datasets.IsNullOrEmpty());
        }

        [TestMethod]
        public async Task GetDatasetsBySQLQuery_WithValidDatasetsQuery_ReturnsDatasets()
        {
            SetUpDatasetTableData();

            var datasets = await _datasetsRepository.GetDatasetsBySQLQuery(_ => _.DatasetId == "DS1");

            Assert.IsTrue(!datasets.IsNullOrEmpty());
            Assert.AreEqual(datasets.First().Id, "DS1");
        }

        [TestMethod]
        public async Task GetDatasetsBySQLQuery_WithValidDatasetsVersionsQuery_ReturnsDatasets()
        {
            SetUpDatasetTableData();

            var datasets = await _datasetsRepository.GetDatasetsBySQLQuery(null, _ => _.DatasetVersionId == "DS1_v1");

            Assert.IsTrue(!datasets.IsNullOrEmpty());
            Assert.AreEqual(datasets.First().Id, "DS1");
        }

        [TestMethod]
        public async Task DeleteDatasetsBySpecificationId_WithValidData_SoftDelete()
        {
            SetUpSpecificationAndDefinitionSpecificationRelationshipTableData();
            SetUpDatasetTableData();
            
            DetachAllDBEntries();

            await _datasetsRepository.DeleteDatasetsBySpecificationId("test", Models.Messages.DeletionType.SoftDelete);

            Assert.IsTrue(_dbContext.Datasets.FirstOrDefault().IsDeleted);

            Assert.IsNull(_dbContext.Datasets.Where(_ => _.IsDeleted).FirstOrDefault());
        }

        [TestMethod]
        public async Task DeleteDatasetsBySpecificationId_WithValidData_PermanentDelete()
        {
            SetUpSpecificationAndDefinitionSpecificationRelationshipTableData();
            SetUpDatasetTableData();

            DetachAllDBEntries();

            await _datasetsRepository.DeleteDatasetsBySpecificationId("test", Models.Messages.DeletionType.PermanentDelete);

            //Support for hard delete has been removed
            Assert.IsNotNull(_dbContext.Datasets.Where(_ => !_.IsDeleted).FirstOrDefault());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task SaveDataset_WithNullData_ThrowArgumentException()
        {

            await _datasetsRepository.SaveDataset(null);

        }

        [TestMethod]
        public async Task SaveDataset_WithValidData()
        {
            SetUpDatasetTables();
            SetUpFundingStreamTable();
            SetUpFundingStreamTableData();

            await _dbContext.SaveChangesAsync();

            CalculateFunding.Models.Datasets.Dataset dataset = new CalculateFunding.Models.Datasets.Dataset()
            {
                RelationshipId = "DSR1",
                Definition = new CalculateFunding.Models.Datasets.DatasetDefinitionVersion()
                {
                    Id = "1780700",
                    Name = "16-19 Test Case",
                    Version = 1,
                },
                Current = new CalculateFunding.Models.Datasets.DatasetVersion()
                {
                    DatasetId = "DS1",
                    BlobName = "DS1/v1/Test_Dataset.xlsx",
                    RowCount = 1,
                    NewRowCount = 1,
                    AmendedRowCount = 1,
                    UploadedBlobFilePath = "DS1/v1/Test_Dataset.xlsx",
                    ChangeType = Models.Datasets.DatasetChangeType.Unknown,
                    FundingStream = new Reference("1619", "16-19"),
                    ProviderVersionId = "",
                    Description = "test",
                    Version = 1,
                    Date = DateTime.Now,
                    Author = new Reference()
                    {
                        Id = "Unknown",
                        Name = "Unknown"
                    },
                    Comment = "Comments",
                    PublishStatus = PublishStatus.Draft
                },
                Id = "DS1",
                Name = "Test_Dataset",
            };

            HttpStatusCode result = await _datasetsRepository.SaveDataset(dataset);

            Assert.AreEqual(HttpStatusCode.OK, result);
        }

        [TestMethod]
        public async Task SaveDataset_WithExistingValidData()
        {
            SetUpDatasetTableData();

            DetachAllDBEntries();

            CalculateFunding.Models.Datasets.Dataset dataset = new CalculateFunding.Models.Datasets.Dataset()
            {
                RelationshipId = "DSR1",
                Definition = new CalculateFunding.Models.Datasets.DatasetDefinitionVersion()
                {
                    Id = "1780700",
                    Name = "16-19 Test Case",
                    Version = 1,
                },
                Current = new CalculateFunding.Models.Datasets.DatasetVersion()
                {
                    DatasetId = "DS1",
                    BlobName = "DS1/v1/Test_Dataset.xlsx",
                    RowCount = 444,
                    NewRowCount = 1,
                    AmendedRowCount = 1,
                    UploadedBlobFilePath = "DS1/v1/Test_Dataset.xlsx",
                    ChangeType = Models.Datasets.DatasetChangeType.Unknown,
                    FundingStream = new Reference("1619", "16-19"),
                    ProviderVersionId = "",
                    Description = "test",
                    Version = 1,
                    Date = DateTime.Now,
                    Author = new Reference()
                    {
                        Id = "Unknown",
                        Name = "Unknown"
                    },
                    Comment = "Comments",
                    PublishStatus = PublishStatus.Draft
                },
                Id = "DS1",
                Name = "Test_Dataset",
            };

            HttpStatusCode result = await _datasetsRepository.SaveDataset(dataset);

            Assert.AreEqual(HttpStatusCode.OK, result);
        }


        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task SaveDatasets_WithNullData_ThrowArgumentException()
        {

            await _datasetsRepository.SaveDatasets(null);

        }

        #endregion

        #region ConverterMergeDataLog Methods

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetConverterDataMergeLog_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _datasetsRepository.GetConverterDataMergeLog(null);
        }

        [TestMethod]
        public async Task GetConverterDataMergeLog_WithInValidData_ReturnsNull()
        {
            SetUpConverterDataMergeLogTableData();

            var dataMergeLog = await _datasetsRepository.GetConverterDataMergeLog("log12");

            Assert.IsNull(dataMergeLog);
        }


        [TestMethod]
        public async Task GetConverterDataMergeLog_WithValidData_ReturnsConverterDataMergeLog()
        {
            SetUpConverterDataMergeLogTableData();

            var dataMergeLog = await _datasetsRepository.GetConverterDataMergeLog("log1");

            Assert.IsNotNull(dataMergeLog);
            Assert.IsInstanceOfType(dataMergeLog, typeof(CalculateFunding.Models.Datasets.Converter.ConverterDataMergeLog));
        }


        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetConverterDataMergeLogsByParentJobId_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _datasetsRepository.GetConverterDataMergeLogsByParentJobId(null);
        }

        [TestMethod]
        public async Task GetConverterDataMergeLogsByParentJobId_WithInValidData_ReturnsNull()
        {
            SetUpConverterDataMergeLogTableData();

            var dataMergeLogs = await _datasetsRepository.GetConverterDataMergeLogsByParentJobId("parentJob12");

            Assert.IsTrue(dataMergeLogs.IsNullOrEmpty());
        }

        [TestMethod]
        public async Task GetConverterDataMergeLogsByParentJobId_WithValidData_ReturnsConverterDataMergeLog()
        {
            SetUpConverterDataMergeLogTableData();

            var dataMergeLog = await _datasetsRepository.GetConverterDataMergeLogsByParentJobId("parentJob1");

            Assert.IsNotNull(dataMergeLog);
            Assert.IsInstanceOfType(dataMergeLog, typeof(List<CalculateFunding.Models.Datasets.Converter.ConverterDataMergeLog>));
        }

        [TestMethod]
        public async Task GetConverterDataMergeLogsBySQLQuery_WithInValidData_ReturnsNull()
        {
            SetUpConverterDataMergeLogTableData();

            var dataMergeLogs = await _datasetsRepository.GetConverterDataMergeLogsBySQLQuery(_ => _.Id == "job12");

            Assert.IsTrue(dataMergeLogs.IsNullOrEmpty());
        }

        [TestMethod]
        public async Task GetConverterDataMergeLogsBySQLQuery_WithValidData_ReturnsConverterDataMergeLog()
        {
            SetUpConverterDataMergeLogTableData();

            var dataMergeLog = await _datasetsRepository.GetConverterDataMergeLogsBySQLQuery(_ => _.ParentJobId == "parentJob1");

            Assert.IsNotNull(dataMergeLog);
            Assert.IsInstanceOfType(dataMergeLog, typeof(List<CalculateFunding.Models.Datasets.Converter.ConverterDataMergeLog>));
        }


        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task SaveConverterDataMergeLog_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _datasetsRepository.SaveConverterDataMergeLog(null);
        }

        [TestMethod]
        public async Task SaveConverterDataMergeLog_WithValidData_SavesLog()
        {
            SetUpConverterDataMergeLogTable();

            var converterDataMergeLogModel = new CalculateFunding.Models.Datasets.Converter.ConverterDataMergeLog()
            {
                JobId = "log1",
                ParentJobId = "parentJob1",
                DatasetVersionCreated = 1,
                Results = new List<CalculateFunding.Models.Datasets.Converter.RowCopyResult>() { },
                Request = new Models.Datasets.Converter.ConverterMergeRequest()
                {
                    ProviderVersionId = "pv1",
                    DatasetId = "ds1",
                    Version = "1",
                    Author = new Reference("test-user", "test-user"),
                    DatasetRelationshipId = "dsr1"
                }
            };

            try
            {
                await _datasetsRepository.SaveConverterDataMergeLog(converterDataMergeLogModel);
                Assert.IsTrue(true);
            }
            catch
            {
                Assert.IsTrue(false);
            }
        }


        #endregion


        #region Private Methods

        private void SetUpDatasetDefinitionTable()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.DatasetDefinition>()).Returns(new GenericRepository<EntityModel.DatasetDefinition>(_dbContext));
        }

        private void SetUpDatasetTables()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Dataset>()).Returns(new GenericRepository<EntityModel.Dataset>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.DatasetVersion>()).Returns(new GenericRepository<EntityModel.DatasetVersion>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.FundingStream>()).Returns(new GenericRepository<EntityModel.FundingStream>(_dbContext));
        }

        private void SetUpSpecificationTables()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Specification>()).Returns(new GenericRepository<EntityModel.Specification>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.SpecificationVersion>()).Returns(new GenericRepository<EntityModel.SpecificationVersion>(_dbContext));
        }

        private void SetUpDefinitionSpecificationRelationshipTables()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.DatasetSpecificationRelationship>()).Returns(new GenericRepository<EntityModel.DatasetSpecificationRelationship>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.DefinitionSpecificationRelationship>()).Returns(new GenericRepository<EntityModel.DefinitionSpecificationRelationship>(_dbContext));
        }

        private void SetUpConverterDataMergeLogTable()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.ConverterDataMergeLog>()).Returns(new GenericRepository<EntityModel.ConverterDataMergeLog>(_dbContext));
        }

        private void SetUpFundingStreamTable()
        {
            _uowMock.Setup(x => x.GenericRepository<FundingStream>()).Returns(new GenericRepository<FundingStream>(_dbContext));
        }

        private async void SetUpFundingStreamTableData()
        {
            var fundingStream1 = new FundingStream()
            {
                FundingStreamId = 1,
                FundingStreamCode = "1619",
                FundingStreamName = "Name",
            };
            _dbContext.FundingStreams.Add(fundingStream1);

            var fundingStream2 = new FundingStream()
            {
                FundingStreamId = 2,
                FundingStreamCode = "GAG",
                FundingStreamName = "Name",
            };
            _dbContext.FundingStreams.Add(fundingStream2);
        }

        private async void SetUpDatasetDefintionTableData()
        {
            SetUpDatasetDefinitionTable();
            SetUpFundingStreamTable();

            SetUpFundingStreamTableData();

            FieldDefinition fieldDefinition = new FieldDefinition()
            {
                Id = "1780702",
                Name = "Field1",
                IdentifierFieldType = IdentifierFieldType.UKPRN,
                Type = FieldType.Integer
            };

            TableDefinition tableDefinition = new TableDefinition()
            {
                Id = "1780701",
                Name = "16-19 Test Case",
                Description = "Description",
                FieldDefinitions = new List<FieldDefinition>() { fieldDefinition }
            };

            var datasetDefinition = new EntityModel.DatasetDefinition()
            {
                Id = "1780700",
                Name = "16-19 Test Case",
                Description = "This dataset covers test case data for 16-19 providers",
                FundingStreamId = 1,
                ValidateProvidersByYearRange = 2,
                ValidateProviders = false,
                ConverterEligible = true,
                IsDeleted = false,
                TableDefinitions = JsonConvert.SerializeObject(new List<TableDefinition>() { tableDefinition })
            };
            _dbContext.DatasetDefinitions.Add(datasetDefinition);

            await _dbContext.SaveChangesAsync();
        }

        private async void SetUpDatasetTableData()
        {

            SetUpDatasetTables();
            SetUpFundingStreamTable();

            SetUpFundingStreamTableData();

            var dataset = new EntityModel.Dataset()
            {
                DatasetId = "DS1",
                Name = "Test Dataset",
                DefinitionId = "1780700",
                DefinitionName = "16-19 Test Case",
                IsDeleted = false
            };
            _dbContext.Datasets.Add(dataset);

            var datasetVersion = new EntityModel.DatasetVersion()
            {
                DatasetVersionId = "DS1_v1",
                DatasetId = "DS1",
                BlobName = "DS1/v1/Test_Dataset.xlsx",
                RowCount = 1,
                NewRowCount = 1,
                AmendedRowCount = 1,
                UploadedBlobFilePath = "DS1/v1/Test_Dataset.xlsx",
                ChangeType = "Unknown",
                FundingStreamId = 1,
                Description = "Unknown",
                Version = 1,
                Date = DateTime.Now,
                AuthorId = "Unknown",
                AuthorName = "Unknown",
                PublishStatus = "Draft",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsLatest = true
            };
            _dbContext.DatasetVersions.Add(datasetVersion);


            await _dbContext.SaveChangesAsync();


        }

        private async void SetUpConverterDataMergeLogTableData()
        {
            SetUpConverterDataMergeLogTable();

            var converterDataMergeLogModel = new CalculateFunding.Models.Datasets.Converter.ConverterDataMergeLog()
            {
                JobId = "log1",
                ParentJobId = "parentJob1",
                DatasetVersionCreated = 1,
                Results = new List<CalculateFunding.Models.Datasets.Converter.RowCopyResult>() { },
                Request = new Models.Datasets.Converter.ConverterMergeRequest()
                {
                    ProviderVersionId = "pv1",
                    DatasetId = "ds1",
                    Version = "1",
                    Author = new Reference("test-user", "test-user"),
                    DatasetRelationshipId = "dsr1"
                }
            };

            var convertedDataMergeLog = new EntityModel.ConverterDataMergeLog(){
                Id = "log1",
                ContentJson = JsonConvert.SerializeObject(converterDataMergeLogModel),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsDeleted = false,
                ParentJobId = "parentJob1"
            };

            _dbContext.ConverterDataMergeLogs.Add(convertedDataMergeLog);

            await _dbContext.SaveChangesAsync();

        }

        private async void SetUpSpecificationAndDefinitionSpecificationRelationshipTableData()
        {
            SetUpSpecificationTables();
            SetUpDefinitionSpecificationRelationshipTables();

            CalculateFunding.Models.Datasets.PublishedSpecificationConfiguration publishedFundingConfiguration = new CalculateFunding.Models.Datasets.PublishedSpecificationConfiguration()
            {
                FundingLines = new List<CalculateFunding.Models.Datasets.PublishedSpecificationItem>() { },
                Calculations = new List<CalculateFunding.Models.Datasets.PublishedSpecificationItem>() { },
                FundingStreamId = "",
                FundingPeriodId = "",
                SpecificationId = "test",
                IncludeCarryForward = false
            };

            for (int i = 1; i <= 3; i++)
            {
                var datasetSpecRelationshipEntry = new EntityModel.DatasetSpecificationRelationship()
                {
                    IsDeleted = false,
                    IsLatest = i == 3,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    PublishStatus = PublishStatus.Approved.ToString(),
                    Version = i,
                    RelationshipType = CalculateFunding.Models.Datasets.DatasetRelationshipType.FDS.ToString(),
                    UsedInDataAggregations = false,
                    ConverterEnabled = false,
                    IsSetAsProviderData = false,
                    Description = "test",
                    SpecificationId = "test",
                    Name = "name",
                    DatasetSpecificationRelationshipId = "1",
                    DatasetSpecificationRelationshipVersionId = i.ToString(),
                    DatasetId = "DS1",
                    AuthorId = "id",
                    AuthorName = "name",
                    DatasetDefinitionId = "1",
                    DatasetDefinitionName = "1-name",
                    PublishedSpecificationConfiguration = JsonConvert.SerializeObject(publishedFundingConfiguration)
                };
                _dbContext.DatasetSpecificationRelationships.Add(datasetSpecRelationshipEntry);
            }

            var specTableEntry = new EntityModel.Specification()
            {
                ForceUpdateOnNextRefresh = true,
                SpecificationId = "test",
                IsDeleted = false,
                SpecificationName = "Test Name",
                FundingPeriodId = 1,
                FundingStreamId = 1,
                IsSelectedForFunding = true,
            };
            _dbContext.Specifications.Add(specTableEntry);

            var specVersionTableEntry = new EntityModel.SpecificationVersion()
            {
                IsDeleted = false,
                IsLatest = true,
                SpecificationVersionId = "v3",
                SpecificationId = "test",
                SpecificationName = "Name-2",
                Version = 3,
                CoreProviderVersionUpdates = "Manual",
                AuthorId = "id",
                AuthorName = "UserName",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Date = DateTime.Now,
                PublishStatus = "Updated",
                ProviderSource = "FDZ"
            };
            _dbContext.SpecificationVersions.Add(specVersionTableEntry);

            var definitionSpecificationRelationshipTableEntry = new EntityModel.DefinitionSpecificationRelationship()
            {
                DataDefinitionRelationshipId = "1",
                Id = 1,
                IsDeleted = false,
                SpecificationVersionId = "v3",

            };

            _dbContext.DefinitionSpecificationRelationships.Add(definitionSpecificationRelationshipTableEntry);
        }


        private void DetachAllDBEntries()
        {
            foreach (var entry in _dbContext.ChangeTracker.Entries())
            {
                entry.State = EntityState.Detached;
            }
        }


        #endregion



    }
}
