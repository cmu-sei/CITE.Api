// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

namespace Cite.Api.Infrastructure.Mappings
{
    using System.Linq;
    using AutoMapper;
    using Cite.Api.Data.Models;
    using Cite.Api.ViewModels;

    public class GroupProfile : AutoMapper.Profile
    {
        public GroupProfile()
        {
            CreateMap<GroupEntity, Group>();
            CreateMap<Group, GroupEntity>();
        }
    }
}
