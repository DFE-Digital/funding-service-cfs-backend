using AutoMapper;
using CalculateFunding.Common.ApiClient.FDS.Models;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Models.Datasets.ViewModels;

namespace CalculateFunding.Services.Datasets.MappingProfiles
{
    public class FDSDatasetsMappingProfile : Profile
    {
        public FDSDatasetsMappingProfile()
        {
            CreateMap<FDSFieldDefinition, FieldDefinition>()
                .ForMember(c => c.Id, opt => opt.ToString());

            CreateMap<FDSTableDefinitions, TableDefinition>()
                .ForMember(m => m.FieldDefinitions, opt => opt.MapFrom(s => s.FDSFieldDefinitions));

            CreateMap<FDSDatasetDefinition, DatasetDefinition>()
                .ForMember(m => m.TableDefinitions, opt => opt.MapFrom(s => s.FDSTableDefinitions));

            CreateMap<FDSDatasetDefinition, DatasetDefinitionViewModel>()
                .ForMember(m => m.IsLatestVersion, opt => opt.MapFrom(s => s.IsActive))
                .ForMember(m => m.LastUpdatedBy, opt => opt.MapFrom(s => s.UpdatedBy))
                .ForMember(m => m.LastUpdatedDt, opt => opt.MapFrom(s => s.UpdatedDt));

        }
    }
}
