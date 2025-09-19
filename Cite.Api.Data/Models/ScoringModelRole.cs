// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.Data.Models;

public class ScoringModelRoleEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public bool AllPermissions { get; set; }

    public List<ScoringModelPermission> Permissions { get; set; }
}

public static class ScoringModelRoleEntityDefaults
{
    public static Guid ScoringModelOwnerRoleId = new("1a3f26cd-9d99-4b98-b914-12931e786199");
    public static Guid ScoringModelEditorRoleId = new("f870d8ee-7332-4f7f-8ee0-63bd07cfd7e5");
    public static Guid ScoringModelObserverRoleId = new("39aa296e-05ba-4fb0-8d74-c92cf3354c60");
}

public class ScoringModelRoleEntityConfiguration : IEntityTypeConfiguration<ScoringModelRoleEntity>
{
    public void Configure(EntityTypeBuilder<ScoringModelRoleEntity> builder)
    {
        builder.HasData(
            new ScoringModelRoleEntity
            {
                Id = ScoringModelRoleEntityDefaults.ScoringModelOwnerRoleId,
                Name = "Owner",
                AllPermissions = true,
                Permissions = [],
                Description = "Can view, edit, and delete the ScoringModel"
            },
            new ScoringModelRoleEntity
            {
                Id = ScoringModelRoleEntityDefaults.ScoringModelEditorRoleId,
                Name = "Editor",
                AllPermissions = false,
                Permissions = [
                    ScoringModelPermission.ViewScoringModel,
                    ScoringModelPermission.EditScoringModel
                ],
                Description = "Can view and edit the ScoringModel"
            },
            new ScoringModelRoleEntity
            {
                Id = ScoringModelRoleEntityDefaults.ScoringModelObserverRoleId,
                Name = "Viewer",
                AllPermissions = false,
                Permissions = [ScoringModelPermission.ViewScoringModel],
                Description = "Can view the ScoringModel"
            }
        );
    }
}
