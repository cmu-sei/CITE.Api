// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Cite.Api.Data.Models;
using Cite.Api.ViewModels;

namespace Cite.Api.Infrastructure.Mappings
{
    public class MoveProfile : AutoMapper.Profile
    {
        public MoveProfile()
        {
            CreateMap<MoveEntity, Move>();

            CreateMap<Move, MoveEntity>()
                .ForMember(m => m.Evaluation, opt => opt.Ignore());

            CreateMap<MoveEntity, MoveEntity>()
                .ForMember(m => m.Id, opt => opt.Ignore())
                .ForMember(m => m.Evaluation, opt => opt.Ignore());

        }
    }
}
