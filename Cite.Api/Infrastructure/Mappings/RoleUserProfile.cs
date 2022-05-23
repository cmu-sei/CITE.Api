// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Services;
using Cite.Api.ViewModels;
using System.Security.Claims;

namespace Cite.Api.Infrastructure.Mappings
{
    public class RoleUserProfile : AutoMapper.Profile
    {
        public RoleUserProfile()
        {
            CreateMap<RoleUserEntity, RoleUser>();

            CreateMap<RoleUser, RoleUserEntity>();
        }
    }
}


