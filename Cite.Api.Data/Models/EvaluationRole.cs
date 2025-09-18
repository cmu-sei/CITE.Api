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
    public static Guid EvaluationEditorRoleId = new("8aaa0d30-bdbe-4f2b-a6b8-f1a5466b2560");
    public static Guid EvaluationViewerRoleId = new("4af74a62-596c-4767-a43a-b74aa8e48526");
    public static Guid EvaluationMemberRoleId = new("f870d8ee-7332-4f7f-8ee0-63bd07cfd7e4");
    public static Guid EvaluationObserverRoleId = new("39aa296e-05ba-4fb0-8d74-c92cf3354c6f");
    public static Guid EvaluationFacilitatorRoleId = new("7c366199-f795-4a04-b360-e8705e77a052");
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
                Description = "Can perform all actions on the Evaluation in administration"
            },
            new EvaluationRoleEntity
            {
                Id = EvaluationRoleDefaults.EvaluationEditorRoleId,
                Name = "Editor",
                AllPermissions = false,
                Permissions = [
                    EvaluationPermission.ViewEvaluation,
                    EvaluationPermission.EditEvaluation
                ],
                Description = "Can edit the Evaluation in administration"
            },
            new EvaluationRoleEntity
            {
                Id = EvaluationRoleDefaults.EvaluationViewerRoleId,
                Name = "Viewer",
                AllPermissions = false,
                Permissions = [
                    EvaluationPermission.ViewEvaluation
                ],
                Description = "Can view the Evaluation in administration"
            },
            new EvaluationRoleEntity
            {
                Id = EvaluationRoleDefaults.EvaluationMemberRoleId,
                Name = "Member",
                AllPermissions = false,
                Permissions = [
                    EvaluationPermission.ParticipateInEvaluation
                ],
                Description = "Has read only access to the Evaluation up to the current move"
            },
            new EvaluationRoleEntity
            {
                Id = EvaluationRoleDefaults.EvaluationObserverRoleId,
                Name = "Observer",
                AllPermissions = false,
                Permissions = [EvaluationPermission.ObserveEvaluation],
                Description = "Has read only access to all teams in the Evaluation up to the current move"
            },
            new EvaluationRoleEntity
            {
                Id = EvaluationRoleDefaults.EvaluationFacilitatorRoleId,
                Name = "Facilitator",
                AllPermissions = false,
                Permissions = [
                    EvaluationPermission.ObserveEvaluation,
                    EvaluationPermission.ExecuteEvaluation
                ],
                Description = "Can observe all teams and advance moves for the Evaluation"
            }
        );
    }
}
