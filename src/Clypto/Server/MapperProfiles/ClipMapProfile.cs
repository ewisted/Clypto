using AutoMapper;
using Clypto.Server.Data.Models;
using Clypto.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Clypto.Server.MapperProfiles
{
    public class ClipMapProfile : Profile
    {
        public ClipMapProfile()
        {
            CreateMap<Clip, ClipDTO>().ForMember(dto =>
                    dto.ClipId,
                    conf => conf.MapFrom(c => c.Id));
        }
    }
}
