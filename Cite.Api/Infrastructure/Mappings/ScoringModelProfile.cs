// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System.Collections.Generic;
using System.Linq;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;

namespace Cite.Api.Infrastructure.Mappings
{
    public class ScoringModelProfile : AutoMapper.Profile
    {
        public ScoringModelProfile()
        {
            CreateMap<ScoringModelEntity, ScoringModel>();

            // a caller that doesn't send severity levels keeps the saved ones
            CreateMap<ScoringModel, ScoringModelEntity>()
                .ForMember(e => e.SeverityLevels, opt => opt.Condition(src => src.SeverityLevels != null));

            CreateMap<ScoringModelFields, ScoringModelEntity>()
                .ForMember(e => e.SeverityLevels, opt => opt.Condition(src => src.SeverityLevels != null))
                .IncludeAllDerived();

            // copies get their own severity levels
            CreateMap<ScoringModelEntity, ScoringModelEntity>()
                .ForMember(e => e.Id, opt => opt.Ignore())
                .ForMember(e => e.ScoringCategories, opt => opt.Ignore())
                .ForMember(e => e.Memberships, opt => opt.Ignore())
                .ForMember(e => e.SeverityLevels, opt => opt.MapFrom(src => (src.SeverityLevels ?? new List<SeverityLevelEntity>()).Select(l => new SeverityLevelEntity
                {
                    Name = l.Name,
                    MinimumScore = l.MinimumScore,
                    Color = l.Color,
                    Description = l.Description,
                }).ToList()));

            CreateMap<SeverityLevelEntity, SeverityLevel>();

            CreateMap<SeverityLevel, SeverityLevelEntity>();

        }
    }
}


