// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using System.Linq;

namespace Cite.Api.Infrastructure.Mappings
{
    public class TeamProfile : AutoMapper.Profile
    {
        public TeamProfile()
        {
            CreateMap<TeamEntity, Team>()
                .ForMember(m => m.Submissions, opt => opt.ExplicitExpansion())
                .ForMember(m => m.Memberships, opt => opt.ExplicitExpansion());

            CreateMap<Team, TeamEntity>()
                .ForMember(m => m.Submissions, opt => opt.Ignore())
                .ForMember(m => m.Memberships, opt => opt.Ignore());

            CreateMap<TeamEntity, TeamEntity>()
                .ForMember(m => m.Id, opt => opt.Ignore())
                .ForMember(m => m.Submissions, opt => opt.Ignore())
                .ForMember(m => m.Memberships, opt => opt.Ignore());
        }
    }
}
