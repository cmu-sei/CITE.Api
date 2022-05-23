// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Cite.Api.Data.Models;
using System.Collections.Generic;

namespace Cite.Api.Infrastructure.Options
{
    public class SeedDataOptions
    {
        public List<PermissionEntity> Permissions { get; set; }
        public List<UserEntity> Users { get; set; }
        public List<UserPermissionEntity> UserPermissions { get; set; }
        public List<TeamTypeEntity> TeamTypes { get; set; }
        public List<TeamEntity> Teams { get; set; }
        public List<TeamUserEntity> TeamUsers { get; set; }
        public List<ScoringModelEntity> ScoringModels { get; set; }
        public List<ScoringCategoryEntity> ScoringCategories { get; set; }
        public List<ScoringOptionEntity> ScoringOptions { get; set; }
        public List<EvaluationEntity> Evaluations { get; set; }
    }
}

