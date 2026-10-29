// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Text.Json.Serialization;
using Cite.Api.Infrastructure.JsonConverters;

namespace Cite.Api.ViewModels
{
    public class TeamMembership : IAuthorizationType
    {
        public Guid Id { get; set; }
        public Guid TeamId { get; set; }
        public Guid? UserId { get; set; }
        // every membership has a role; a request that leaves it out or sends null gets the Member role
        [JsonConverter(typeof(JsonNullAsEmptyGuidConverter))]
        public Guid RoleId { get; set; }
        public virtual TeamRole Role { get; set; }
    }
}
