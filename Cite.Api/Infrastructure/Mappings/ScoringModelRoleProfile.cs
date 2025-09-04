// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using AutoMapper;
using Cite.Api.ViewModels;
using Cite.Api.Data.Models;

namespace Cite.Api.Infrastructure.Mapping
{
    public class ScoringModelRoleProfile : Profile
    {
        public ScoringModelRoleProfile()
        {
            CreateMap<ScoringModelRoleEntity, ScoringModelRole>();
        }
    }
}
