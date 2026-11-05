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

    [Fact]
    public void A_system_role_maps_its_permissions_both_ways()
    {
        var role = TestData.SystemRole(permissions: [Cite.Api.Data.Enumerations.SystemPermission.ViewUsers]);

        var back = role.ToSystemRole().ToEntity();

        Assert.Equal(role.Permissions, back.Permissions);
    }

    [Fact]
    public void A_duty_maps_its_assigned_users()
    {
        var user = TestData.User(name: "Assignee");
        var duty = TestData.Duty(Guid.NewGuid(), Guid.NewGuid());
        var assignment = TestData.DutyUser(duty.Id, user.Id);
        assignment.User = user;
        duty.DutyUsers.Add(assignment);

        var model = duty.ToDuty();

        Assert.Equal("Assignee", Assert.Single(model.Users).Name);
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

    /// <summary>A scoring model update leaves saved severity levels alone when it carries none, and replaces them when it does.</summary>
    [Fact]
    public void A_scoring_model_update_replaces_the_severity_levels_only_when_it_carries_some()
    {
        var entity = new ScoringModelEntity { SeverityLevels = [new() { Name = "Kept" }] };

        new UpdateScoringModelRequest { SeverityLevels = null }.ApplyTo(entity);
        Assert.Equal("Kept", Assert.Single(entity.SeverityLevels).Name);

        new UpdateScoringModelRequest { SeverityLevels = [new() { Name = "Sent" }] }.ApplyTo(entity);
        Assert.Equal("Sent", Assert.Single(entity.SeverityLevels).Name);
    }

    /// <summary>A copy is a new object, its severity levels too, with every member but the id.</summary>
    [Fact]
    public void A_scoring_model_copy_has_its_own_severity_levels_and_no_id()
    {
        var level = new SeverityLevelEntity { Name = "Calm", MinimumScore = 1, Color = "#FFFFFF", Description = "Quiet" };
        var original = new ScoringModelEntity { Id = Guid.NewGuid(), Description = "Original", SeverityLevels = [level] };

        var copy = original.ToCopy();

        Assert.Equal((Guid.Empty, "Original"), (copy.Id, copy.Description));
        var copied = Assert.Single(copy.SeverityLevels);
        Assert.NotSame(level, copied);
        Assert.Equal(("Calm", 1.0, "#FFFFFF", "Quiet"), (copied.Name, copied.MinimumScore, copied.Color, copied.Description));
    }
}
