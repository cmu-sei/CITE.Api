// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using System.Linq;

namespace Cite.Api.Infrastructure.Mappings
{
    public class GroupProfile : AutoMapper.Profile
    {
        public GroupProfile()
        {
            CreateMap<GroupEntity, Group>()
                .ForMember(m => m.Teams, opt => opt.MapFrom(x => x.GroupTeams.Select(y => y.Team)))
                .ForMember(m => m.Teams, opt => opt.ExplicitExpansion());
            CreateMap<Group, GroupEntity>()
                .ForMember(m => m.GroupTeams, opt => opt.Ignore());
        }
    }
}


