// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Cite.Api.Data.Models;
using Cite.Api.ViewModels;

namespace Cite.Api.Infrastructure.Mappings
{
    public class EvaluationProfile : AutoMapper.Profile
    {
        public EvaluationProfile()
        {
            CreateMap<EvaluationEntity, Evaluation>();

            CreateMap<Evaluation, EvaluationEntity>()
                .ForMember(e => e.Teams, opt => opt.Ignore())
                .ForMember(e => e.Moves, opt => opt.Ignore());

            CreateMap<EvaluationEntity, EvaluationEntity>()
                .ForMember(e => e.Id, opt => opt.Ignore());

        }
    }
}


