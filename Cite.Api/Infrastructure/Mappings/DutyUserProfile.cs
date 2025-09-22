// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Cite.Api.Data.Models;
using Cite.Api.ViewModels;

namespace Cite.Api.Infrastructure.Mappings
{
    public class DutyUserProfile : AutoMapper.Profile
    {
        public DutyUserProfile()
        {
            CreateMap<DutyUserEntity, DutyUser>();

            CreateMap<DutyUser, DutyUserEntity>();
        }
    }
}
