// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using AutoMapper;
using Cite.Api.Data.Models;
using Cite.Api.ViewModels;
using System.Linq;

namespace Cite.Api.Infrastructure.Mappings
{
    public class UserProfile : AutoMapper.Profile
    {
        public UserProfile()
        {
            CreateMap<UserEntity, User>()
                .ForMember(m => m.Submissions, opt => opt.ExplicitExpansion());
            CreateMap<User, UserEntity>()
                .ForMember(m => m.Submissions, opt => opt.Ignore())
                .ForMember(m => m.TeamMemberships, opt => opt.Ignore());
        }
    }
}
