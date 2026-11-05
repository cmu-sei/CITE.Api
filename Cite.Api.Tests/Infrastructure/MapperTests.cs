// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Mappings;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Infrastructure;

/// <summary>
/// The Mapperly mappers called directly, for the rules no endpoint reaches on its own. The build checks that
/// every member is mapped (see .editorconfig); these check what the mapping does with it.
/// </summary>
public class MapperTests
{
    /// <summary>A null nullable source leaves a non-nullable target with its default (MapperDefaults).</summary>
    [Fact]
    public void A_null_nullable_source_keeps_the_non_nullable_targets_default()
    {
        var entity = new TeamMembership { Id = Guid.NewGuid(), TeamId = Guid.NewGuid(), UserId = null }.ToEntity();

        Assert.Equal(Guid.Empty, entity.UserId);
    }

    /// <summary>A submission without an evaluation is read with an empty evaluation id.</summary>
    [Fact]
    public void A_submission_without_an_evaluation_is_read_with_an_empty_evaluation_id()
    {
        var submission = new SubmissionEntity { Id = Guid.NewGuid(), EvaluationId = null }.ToSubmission();

        Assert.Equal(Guid.Empty, submission.EvaluationId);
    }

    /// <summary>A system role is read with the permissions it is stored with.</summary>
    [Fact]
    public void A_system_role_is_read_with_its_permissions()
    {
        var role = TestData.SystemRole(permissions: [Cite.Api.Data.Enumerations.SystemPermission.ViewUsers]);

        Assert.Equal([Cite.Api.Data.Enumerations.SystemPermission.ViewUsers], role.ToSystemRole().Permissions);
    }

    /// <summary>An assignment whose user is not loaded is a null user, not a missing one.</summary>
    [Fact]
    public void A_duty_maps_an_assignment_without_a_loaded_user_to_a_null_user()
    {
        var duty = TestData.Duty(Guid.NewGuid(), Guid.NewGuid());
        duty.DutyUsers.Add(TestData.DutyUser(duty.Id, Guid.NewGuid()));

        Assert.Null(Assert.Single(duty.ToDuty().Users));
    }

    /// <summary>Lists that are missing on the source are empty on the target, as AutoMapper left them.</summary>
    [Fact]
    public void Missing_lists_are_mapped_to_empty_ones()
    {
        var team = new TeamEntity { Memberships = null, Submissions = null }.ToTeam();
        var evaluation = new EvaluationEntity { Teams = null, Moves = null, Submissions = null }.ToEvaluation();
        var model = new ScoringModelEntity { ScoringCategories = null, SeverityLevels = null }.ToScoringModel();
        var duty = new DutyEntity { DutyUsers = null }.ToDuty();
        var category = new ScoringCategory { ScoringOptions = null }.ToEntity();
        var copy = new ScoringModelEntity { SeverityLevels = null, Submissions = null }.ToCopy();

        Assert.Equal(0, team.Memberships.Count + team.Submissions.Count);
        Assert.Equal(0, evaluation.Teams.Count + evaluation.Moves.Count + evaluation.Submissions.Count);
        Assert.Equal(0, model.ScoringCategories.Count + model.SeverityLevels.Count);
        Assert.Empty(duty.Users);
        Assert.Empty(category.ScoringOptions);
        Assert.Equal(0, copy.SeverityLevels.Count + copy.Submissions.Count);
    }

    /// <summary>A scoring model copy keeps the original's audit fields, as AutoMapper's copy did, until the save overwrites them.</summary>
    [Fact]
    public void A_scoring_model_copy_keeps_the_originals_audit_fields()
    {
        var original = new ScoringModelEntity
        {
            DateCreated = TestData.DefaultDateCreated,
            DateModified = TestData.DefaultDateCreated.AddDays(1),
            CreatedBy = Guid.NewGuid(),
            ModifiedBy = Guid.NewGuid(),
        };

        var copy = original.ToCopy();

        Assert.Equal((original.DateCreated, original.DateModified, original.CreatedBy, original.ModifiedBy),
            (copy.DateCreated, copy.DateModified, copy.CreatedBy, copy.ModifiedBy));
    }
}
