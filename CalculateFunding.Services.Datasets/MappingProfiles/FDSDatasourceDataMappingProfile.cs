using AutoMapper;
using CalculateFunding.Common.ApiClient.FDS.Models;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Models.Datasets.ViewModels;

namespace CalculateFunding.Services.Datasets.MappingProfiles
{
    public class FDSDatasourceDataMappingProfile : Profile
    {
        public FDSDatasourceDataMappingProfile()
        {
            CreateMap<Row, RowLoadResult>()
               .ForMember(c => c.Identifier, opt => opt.MapFrom(s => s.Identifier));

            CreateMap<Row, RowLoadResult>()
              .ForMember(c => c.Identifier, opt => opt.MapFrom(s => s.Identifier));

            CreateMap<FDSDatasourceDataModel, TableLoadResult>()
                .ForMember(m => m.TableDefinition, opt => opt.Ignore())
                .ForMember(m => m.Rows, opt => opt.MapFrom(s => s.Rows));


        }
    }
}
