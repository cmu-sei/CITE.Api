// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using System.Linq;

namespace Cite.Api.Infrastructure.Mappings
{
    public class RoleProfile : AutoMapper.Profile
    {
        public RoleProfile()
        {
            CreateMap<RoleEntity, Role>()
                .ForMember(m => m.Users, opt => opt.MapFrom(x => x.RoleUsers.Select(y => y.User)))
                .ForMember(m => m.Users, opt => opt.ExplicitExpansion());

            CreateMap<Role, RoleEntity>()
                .ForMember(m => m.RoleUsers, opt => opt.Ignore());

        }
    }
}


