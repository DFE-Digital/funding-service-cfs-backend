using AutoMapper;
using CalculateFunding.Common.ApiClient.FDS.Models;
using CalculateFunding.Models.Datasets.Schema;

namespace CalculateFunding.Services.Calcs.MappingProfiles
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

        }
    }
}
