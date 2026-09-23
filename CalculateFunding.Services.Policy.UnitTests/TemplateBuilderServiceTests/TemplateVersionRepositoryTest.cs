using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Services.Policy.TemplateBuilder;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Common.EfCore.GenericRepository;
using CalculateFunding.Models.Versioning;
using TemplateBuilderModels = CalculateFunding.Models.Policy;
using CalculateFunding.Common.Models;
using System.Net;
using CalculateFunding.Services.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CalculateFunding.Services.Policy.TemplateBuilderServiceTests
{
    [TestClass]
    public class TemplateVersionRepositoryTest
    {
        protected Mock<IUnitOfWork> _uowMock;
        protected Mock<Serilog.ILogger> _logger;
        protected Mock<ICosmosRepository> _cosmosRepository;
        protected Mock<IConfiguration> _configuration;
        protected ITemplateVersionRepository _templateVersionRepository;
        private readonly Guid templateId = Guid.NewGuid();
        private readonly Guid templateMappingId = Guid.NewGuid();
        protected Mock<INewVersionBuilderFactory<TemplateBuilderModels.TemplateBuilder.TemplateVersion>> _newVersionBuilderFactory;

        public TemplateVersionRepositoryTest()
        {
            _cosmosRepository = new Mock<ICosmosRepository>(MockBehavior.Strict);
            _configuration = new Mock<IConfiguration>(MockBehavior.Strict);
            _uowMock = new Mock<IUnitOfWork>(MockBehavior.Strict);
            _logger = new Mock<Serilog.ILogger>();
            _newVersionBuilderFactory = new Mock<INewVersionBuilderFactory<TemplateBuilderModels.TemplateBuilder.TemplateVersion>>(MockBehavior.Strict);
            _templateVersionRepository = new TemplateVersionRepository(_cosmosRepository.Object, _newVersionBuilderFactory.Object, _configuration.Object, _uowMock.Object, _logger.Object);
        }

        #region CreateInMemoryDb
        public CfsDbContext CreateContextInMemory()
        {

            var option = new DbContextOptionsBuilder<CfsDbContext>().UseInMemoryDatabase(databaseName: "CfsDb")
                .Options;
            var context = new CfsDbContext(option);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            return context;
        }
        #endregion

        #region DataSetup
        List<Template> templates => new List<Template>
            {
                 new Template
                {
                    TemplateId  = templateId,
                    AuthorId = "Test User1",
                    AuthorName ="User",
                    Comment ="Draft Template",
                    CreatedAt = DateTime.Now,
                    Date = DateTime.Now,
                    Description = "Description",
                    FundingPeriodId = 1,
                    FundingStreamId = 1,
                    MajorVersion = 1,
                    MinorVersion = 0,
                    Name = "Name",
                    PublishStatus = "Draft",
                    IsDeleted = false,
                    SchemaVersion = "1.0",
                    Status = "Draft",
                    TemplateJson = "{ \"Lorem\": \"ipsum\" }",
                    UpdatedAt = DateTime.Now,
                    Version = 1,
                    EntityId = "template-1.2-1619"
                }

            };

        List<TemplateVersion> templateVersion => new List<TemplateVersion>
            {
                new TemplateVersion
                {
                    TemplateId = templateId,
                    TemplateVersionId  = $"templateVersion-{templateId}-{1}",
                    AuthorId = "Test User1",
                    AuthorName ="User",
                    Comment ="Draft Template",
                    CreatedAt = DateTime.Now,
                    Date = DateTime.Now,
                    Description = "Description",
                    FundingPeriodId = 1,
                    FundingStreamId = 1,
                    MajorVersion = 1,
                    MinorVersion = 0,
                    Name = "Name",
                    PublishStatus = "Draft",
                    IsDeleted = false,
                    SchemaVersion = "1.0",
                    Status = "Draft",
                    TemplateJson = "{ \"Lorem\": \"ipsum\" }",
                    UpdatedAt = DateTime.Now,
                    Version = 1,
                    EntityId = "template-1.2-1619"
                },

            };

        List<TemplatePredecessor> templatePredecessors => new List<TemplatePredecessor>
            {
                new TemplatePredecessor
                {
                    TemplateId= templateId,
                    TemplateVersionId = $"templateVersion-{templateId}-{1}"
                },
                new TemplatePredecessor
                {
                     TemplateId= templateId,
                    TemplateVersionId = $"templateVersion-{templateId}-{2}"
                }
            };

        List<FundingStream> fundingStreams => new List<FundingStream>
            {
                new FundingStream
                {
                    FundingStreamId= 1,
                    FundingStreamCode = "Test_1619",
                    FundingStreamName = "1619",
                    ShortFundingStreamName ="16-19"
                },
                new FundingStream
                {
                    FundingStreamId= 2,
                    FundingStreamCode = "Test_GAG",
                    FundingStreamName = "GAG",
                    ShortFundingStreamName ="GAG"
                }
            };
        List<FundingPeriod> fundingPeriods => new List<FundingPeriod>
            {
                new FundingPeriod
                {
                    FundingPeriodId= 1,
                    FundingPeriodCode = "AS-2223",
                    FundingPeriodName = "Academies and Schools Year 2022-23",
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddYears(1),
                    Period = "2223",
                    Type ="AS",
                    StartYear = DateTime.Now.Year,
                    EndYear = DateTime.Now.Year + 1,


                },
                new FundingPeriod
                {
                    FundingPeriodId= 2,
                    FundingPeriodCode = "AY-2223",
                    FundingPeriodName = "Academies and Year 2022-23",
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddYears(1),
                    Period = "2223",
                    Type ="AY",
                    StartYear = DateTime.Now.Year,
                    EndYear = DateTime.Now.Year + 1,
                }
            };

        TemplateBuilderModels.TemplateBuilder.Template template => new TemplateBuilderModels.TemplateBuilder.Template
        {
            TemplateId = templateMappingId.ToString(),
            Current = templateVersionData,
            Description = "Description",
            Name = "Template Name",
            FundingStream = fundingStream,
            FundingPeriod = fundingPeriod,
        };

        TemplateBuilderModels.TemplateBuilder.TemplateVersion templateVersionData => new TemplateBuilderModels.TemplateBuilder.TemplateVersion
        {
            TemplateId = templateMappingId.ToString(),
            Author = author,
            Comment = "Draft Template",
            Date = DateTime.Now,
            FundingPeriodId = "AS-2223",
            FundingStreamId = "Test_1619",
            MajorVersion = 1,
            MinorVersion = 0,
            Name = "Template Name",
            PublishStatus = PublishStatus.Draft,
            SchemaVersion = "1.0",
            Status = TemplateBuilderModels.TemplateBuilder.TemplateStatus.Draft,
            TemplateJson = "{ \"Lorem\": \"ipsum\" }",
            Version = 1,
        };
        Reference author => new Reference
        {
            Id = "Test User1",
            Name = "Author",
        };
        CalculateFunding.Models.Policy.FundingStream fundingStream => new CalculateFunding.Models.Policy.FundingStream
        {
            Id = "1",
            Name = "Test_1619",
            ShortName = "16-19"
        };

        CalculateFunding.Models.Policy.FundingPeriod fundingPeriod => new CalculateFunding.Models.Policy.FundingPeriod
        {
            Id = "1",
            Name = "Test_1619",
            StartDate = DateTime.Now,
            EndDate = DateTime.Now,
            Period = "2223",
            Type = TemplateBuilderModels.FundingPeriodType.AY,

        };

        private List<TemplateBuilderModels.TemplateBuilder.TemplateStatus> templateStatus = Enum.GetValues(typeof(TemplateBuilderModels.TemplateBuilder.TemplateStatus)).Cast<TemplateBuilderModels.TemplateBuilder.TemplateStatus>().ToList();


        private TemplateBuilderModels.TemplateBuilder.FindTemplateVersionQuery findTemplateVersionQuery => new TemplateBuilderModels.TemplateBuilder.FindTemplateVersionQuery
        {
            FundingPeriodId = "AS-2223",
            FundingStreamId = "Test_1619",
            Statuses = templateStatus
        };

        #endregion

        [TestMethod]
        public void GetTemplateVersion_Result_NotNull_OR_If_Any_Exception()
        {
            var _context = CreateContextInMemory();
            _uowMock.Setup(x => x.GenericRepository<Template>()).Returns(new GenericRepository<Template>(_context));
            _uowMock.Setup(x => x.GenericRepository<TemplatePredecessor>()).Returns(new GenericRepository<TemplatePredecessor>(_context));
            _uowMock.Setup(x => x.GenericRepository<TemplateVersion>()).Returns(new GenericRepository<TemplateVersion>(_context));
            _uowMock.Setup(x => x.GenericRepository<FundingStream>()).Returns(new GenericRepository<FundingStream>(_context));
            _uowMock.Setup(x => x.GenericRepository<FundingPeriod>()).Returns(new GenericRepository<FundingPeriod>(_context));

            _context.Templates.AddRange(templates);
            _context.TemplateVersions.AddRange(templateVersion);
            _context.TemplatePredecessors.AddRange(templatePredecessors);
            _context.FundingStreams.AddRange(fundingStreams);
            _context.FundingPeriods.AddRange(fundingPeriods);
            _context.SaveChanges();
            _uowMock.Setup(u => u.Commit());

            ////Arrange
            var configurationSectionMock = new Mock<IConfigurationSection>();

            configurationSectionMock
               .Setup(x => x.Value)
               .Returns("true");

            _configuration
               .Setup(x => x.GetSection("UseSQLDB"))
               .Returns(configurationSectionMock.Object);

            //Act
            var result = _templateVersionRepository.GetTemplateVersion(templateId.ToString(), 1);

            //Assert
            if (result.Exception != null)
            {
                Assert.Fail(string.Format("Unexpected exception of type {0} caught: {1}",
                        result.Exception.GetType(), result.Exception.Message));
            }

            if (result.Result != null)
            {

                Assert.IsNotNull(result.Result);
            }

        }

        [TestMethod]
        public void GetSummaryVersionsByTemplate_Result_NotNull_OR_If_Any_Exception()
        {
            var _context = CreateContextInMemory();
            _uowMock.Setup(x => x.GenericRepository<Template>()).Returns(new GenericRepository<Template>(_context));
            _uowMock.Setup(x => x.GenericRepository<TemplatePredecessor>()).Returns(new GenericRepository<TemplatePredecessor>(_context));
            _uowMock.Setup(x => x.GenericRepository<TemplateVersion>()).Returns(new GenericRepository<TemplateVersion>(_context));
            _uowMock.Setup(x => x.GenericRepository<FundingStream>()).Returns(new GenericRepository<FundingStream>(_context));
            _uowMock.Setup(x => x.GenericRepository<FundingPeriod>()).Returns(new GenericRepository<FundingPeriod>(_context));

            _context.Templates.AddRange(templates);
            _context.TemplateVersions.AddRange(templateVersion);
            _context.TemplatePredecessors.AddRange(templatePredecessors);
            _context.FundingStreams.AddRange(fundingStreams);
            _context.FundingPeriods.AddRange(fundingPeriods);
            _context.SaveChanges();
            _uowMock.Setup(u => u.Commit());

            ////Arrange
            var configurationSectionMock = new Mock<IConfigurationSection>();

            configurationSectionMock
               .Setup(x => x.Value)
               .Returns("true");

            _configuration
               .Setup(x => x.GetSection("UseSQLDB"))
               .Returns(configurationSectionMock.Object);

            //Act
            var result = _templateVersionRepository.GetSummaryVersionsByTemplate(templateId.ToString(), templateStatus);

            //Assert
            if (result.Exception != null)
            {
                Assert.Fail(string.Format("Unexpected exception of type {0} caught: {1}",
                        result.Exception.GetType(), result.Exception.Message));
            }

            if (result.Result != null)
            {

                Assert.IsNotNull(result.Result);
            }

        }

        [TestMethod]
        public void FindByFundingStreamAndPeriod_Result_NotNull_OR_If_Any_Exception()
        {
            var _context = CreateContextInMemory();
            _uowMock.Setup(x => x.GenericRepository<Template>()).Returns(new GenericRepository<Template>(_context));
            _uowMock.Setup(x => x.GenericRepository<TemplatePredecessor>()).Returns(new GenericRepository<TemplatePredecessor>(_context));
            _uowMock.Setup(x => x.GenericRepository<TemplateVersion>()).Returns(new GenericRepository<TemplateVersion>(_context));
            _uowMock.Setup(x => x.GenericRepository<FundingStream>()).Returns(new GenericRepository<FundingStream>(_context));
            _uowMock.Setup(x => x.GenericRepository<FundingPeriod>()).Returns(new GenericRepository<FundingPeriod>(_context));

            _context.Templates.AddRange(templates);
            _context.TemplateVersions.AddRange(templateVersion);
            _context.TemplatePredecessors.AddRange(templatePredecessors);
            _context.FundingStreams.AddRange(fundingStreams);
            _context.FundingPeriods.AddRange(fundingPeriods);
            _context.SaveChanges();
            _uowMock.Setup(u => u.Commit());

            ////Arrange
            var configurationSectionMock = new Mock<IConfigurationSection>();

            configurationSectionMock
               .Setup(x => x.Value)
               .Returns("true");

            _configuration
               .Setup(x => x.GetSection("UseSQLDB"))
               .Returns(configurationSectionMock.Object);

            //Act
            var result = _templateVersionRepository.FindByFundingStreamAndPeriod(findTemplateVersionQuery);

            //Assert
            if (result.Exception != null)
            {
                Assert.Fail(string.Format("Unexpected exception of type {0} caught: {1}",
                        result.Exception.GetType(), result.Exception.Message));
            }

            if (result.Result != null)
            {

                Assert.IsNotNull(result.Result);
            }

        }

        [TestMethod]
        public void Save_TemplateVersion_Successful_Result_OR_If_Any_Exception()
        {
            var _context = CreateContextInMemory();
            _uowMock.Setup(x => x.GenericRepository<TemplateVersion>()).Returns(new GenericRepository<TemplateVersion>(_context));
            _uowMock.Setup(x => x.GenericRepository<FundingStream>()).Returns(new GenericRepository<FundingStream>(_context));
            _uowMock.Setup(x => x.GenericRepository<FundingPeriod>()).Returns(new GenericRepository<FundingPeriod>(_context));

            _context.FundingStreams.AddRange(fundingStreams);
            _context.FundingPeriods.AddRange(fundingPeriods);
            _context.SaveChanges();
            _uowMock.Setup(u => u.Commit());

            ////Arrange
            var configurationSectionMock = new Mock<IConfigurationSection>();

            configurationSectionMock
               .Setup(x => x.Value)
               .Returns("true");

            _configuration
               .Setup(x => x.GetSection("UseSQLDB"))
               .Returns(configurationSectionMock.Object);

            //Upadate Mapping Data
            TemplateBuilderModels.TemplateBuilder.TemplateVersion templateUpdatedData = new TemplateBuilderModels.TemplateBuilder.TemplateVersion();
            templateUpdatedData.TemplateId = templateId.ToString();
            templateUpdatedData = templateVersionData;
            templateUpdatedData.TemplateId = templateId.ToString();
            templateUpdatedData.Name = "Template New Name";

            //Act
            var result = _templateVersionRepository.SaveTemplateVersion(templateUpdatedData);

            //Assert
            if (result.Exception != null)
            {
                Assert.Fail(string.Format("Unexpected exception of type {0} caught: {1}",
                        result.Exception.GetType(), result.Exception.Message));
            }

            if (result.Result == HttpStatusCode.Accepted)
            {
                //Get Updated TemplateVersion Data
                var getTemplateResult = _templateVersionRepository.GetTemplateVersion(templateUpdatedData.TemplateId, 1).Result;

                Assert.IsNotNull(getTemplateResult);
                Assert.AreEqual(templateUpdatedData.Name, getTemplateResult.Name);
            }
        }

    }
}
