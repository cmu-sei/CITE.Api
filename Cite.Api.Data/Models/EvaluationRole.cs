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

public class EvaluationRoleEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public bool AllPermissions { get; set; }

    public List<EvaluationPermission> Permissions { get; set; }
}

public static class EvaluationRoleDefaults
{
    public static Guid EvaluationCreatorRoleId = new("1a3f26cd-9d99-4b98-b914-12931e786198");
    public static Guid EvaluationReadOnlyRoleId = new("39aa296e-05ba-4fb0-8d74-c92cf3354c6f");
    public static Guid EvaluationMemberRoleId = new("f870d8ee-7332-4f7f-8ee0-63bd07cfd7e4");
}

public class EvaluationRoleConfiguration : IEntityTypeConfiguration<EvaluationRoleEntity>
{
    public void Configure(EntityTypeBuilder<EvaluationRoleEntity> builder)
    {
        builder.HasData(
            new EvaluationRoleEntity
            {
                Id = EvaluationRoleDefaults.EvaluationCreatorRoleId,
                Name = "Manager",
                AllPermissions = true,
                Permissions = [],
                Description = "Can perform all actions on the Evaluation"
            },
            new EvaluationRoleEntity
            {
                Id = EvaluationRoleDefaults.EvaluationReadOnlyRoleId,
                Name = "Observer",
                AllPermissions = false,
                Permissions = [EvaluationPermission.ViewEvaluation],
                Description = "Has read only access to the Evaluation"
            },
            new EvaluationRoleEntity
            {
                Id = EvaluationRoleDefaults.EvaluationMemberRoleId,
                Name = "Member",
                AllPermissions = false,
                Permissions = [
                    EvaluationPermission.ViewEvaluation,
                    EvaluationPermission.EditEvaluation
                ],
                Description = "Has read only access to the Evaluation"
            }
        );
    }
}
