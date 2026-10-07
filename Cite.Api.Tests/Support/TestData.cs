// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

// cite.api: the roles are seeded by HasData in Cite.Api.Data's entity configurations, so the ids below are
// the production constants those calls use. appsettings.json's SeedData section is commented out and runs
// only in InitializeDatabase, which the template database never sees.

using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;

namespace Cite.Api.Tests.Support;

/// <summary>Object mothers, and the ids of the rows the migrations seed.</summary>
public static class TestData
{
    /// <summary>Ids of the system roles the migrations seed (<c>SystemRoleEntityConfiguration</c>).</summary>
    public static class Roles
    {
        public static readonly Guid Administrator = SystemRoleEntityDefaults.AdministratorRoleId;
        public static readonly Guid ContentDeveloper = SystemRoleEntityDefaults.ContentDeveloperRoleId;
        public static readonly Guid Observer = SystemRoleEntityDefaults.ObserverRoleId;
    }

    /// <summary>Ids of the evaluation roles the migrations seed (<c>EvaluationRoleConfiguration</c>).</summary>
    public static class EvaluationRoles
    {
        public static readonly Guid Owner = EvaluationRoleDefaults.EvaluationOwnerRoleId;
        public static readonly Guid Member = EvaluationRoleDefaults.EvaluationMemberRoleId;
    }

    /// <summary>Ids of the scoring model roles the migrations seed.</summary>
    public static class ScoringModelRoles
    {
        public static readonly Guid Owner = ScoringModelRoleEntityDefaults.ScoringModelOwnerRoleId;
        public static readonly Guid Editor = ScoringModelRoleEntityDefaults.ScoringModelEditorRoleId;
    }

    /// <summary>Ids of the team roles the migrations seed (<c>TeamRoleConfiguration</c>).</summary>
    public static class TeamRoles
    {
        public static readonly Guid Owner = TeamRoleDefaults.TeamOwnerRoleId;
        public static readonly Guid Member = TeamRoleDefaults.TeamMemberRoleId;
    }

    /// <summary>A fixed creation timestamp. Tests that care about ordering pass their own.</summary>
    public static readonly DateTime DefaultDateCreated = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static UserEntity User(Guid? id = null, string name = "Test User", Guid? roleId = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            RoleId = roleId
        };

    /// <summary>A system role of its own, named uniquely because role names are uniquely indexed.</summary>
    public static SystemRoleEntity SystemRole(bool allPermissions = false, SystemPermission[] permissions = null)
    {
        var id = Guid.NewGuid();

        return SystemRole($"role-{id:N}", allPermissions, permissions, id);
    }

    /// <summary>A system role with the name a test chose, for the tests about <c>system_roles.name</c>.</summary>
    public static SystemRoleEntity SystemRole(
        string name,
        bool allPermissions = false,
        SystemPermission[] permissions = null,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            AllPermissions = allPermissions,
            Permissions = [.. permissions ?? []]
        };

    /// <summary>An evaluation role granting exactly <paramref name="permissions"/>.</summary>
    public static EvaluationRoleEntity EvaluationRole(params EvaluationPermission[] permissions)
    {
        var id = Guid.NewGuid();

        return new EvaluationRoleEntity { Id = id, Name = $"evaluation-role-{id:N}", Permissions = [.. permissions] };
    }

    /// <summary>A scoring model role granting exactly <paramref name="permissions"/>.</summary>
    public static ScoringModelRoleEntity ScoringModelRole(params ScoringModelPermission[] permissions)
    {
        var id = Guid.NewGuid();

        return new ScoringModelRoleEntity { Id = id, Name = $"scoring-model-role-{id:N}", Permissions = [.. permissions] };
    }

    /// <summary>A team role granting exactly <paramref name="permissions"/>.</summary>
    public static TeamRoleEntity TeamRole(params TeamPermission[] permissions)
    {
        var id = Guid.NewGuid();

        return new TeamRoleEntity { Id = id, Name = $"team-role-{id:N}", Permissions = [.. permissions] };
    }

    /// <summary>A scoring model template (no evaluation), active.</summary>
    public static ScoringModelEntity ScoringModel(string description = "Test Scoring Model", Guid? evaluationId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Description = description,
            Status = ItemStatus.Active,
            CalculationEquation = "{sum}",
            EvaluationId = evaluationId,
            DateCreated = DefaultDateCreated
        };

    /// <summary>An active evaluation on move 0, scored with <paramref name="scoringModelId"/>.</summary>
    public static EvaluationEntity Evaluation(Guid scoringModelId, string description = "Test Evaluation") =>
        new()
        {
            Id = Guid.NewGuid(),
            Description = description,
            Status = ItemStatus.Active,
            CurrentMoveNumber = 0,
            SituationTime = DefaultDateCreated,
            ScoringModelId = scoringModelId,
            DateCreated = DefaultDateCreated
        };

    public static TeamTypeEntity TeamType(string name = "Test Team Type", bool isOfficialScoreContributor = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsOfficialScoreContributor = isOfficialScoreContributor
        };

    public static TeamEntity Team(Guid? evaluationId, Guid teamTypeId, string name = "Test Team") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ShortName = name,
            EvaluationId = evaluationId,
            TeamTypeId = teamTypeId
        };

    public static MoveEntity Move(Guid evaluationId, int moveNumber = 0, string description = "Test Move") =>
        new()
        {
            Id = Guid.NewGuid(),
            Description = description,
            MoveNumber = moveNumber,
            SituationTime = DefaultDateCreated,
            EvaluationId = evaluationId
        };

    public static ScoringCategoryEntity ScoringCategory(Guid scoringModelId, string description = "Test Category", int displayOrder = 1) =>
        new()
        {
            Id = Guid.NewGuid(),
            Description = description,
            DisplayOrder = displayOrder,
            CalculationEquation = "{sum}",
            ScoringWeight = 1,
            MoveNumberFirstDisplay = 0,
            MoveNumberLastDisplay = 10,
            ScoringModelId = scoringModelId
        };

    public static ScoringOptionEntity ScoringOption(Guid scoringCategoryId, string description = "Test Option", double value = 1, int displayOrder = 1) =>
        new()
        {
            Id = Guid.NewGuid(),
            Description = description,
            DisplayOrder = displayOrder,
            Value = value,
            ScoringCategoryId = scoringCategoryId
        };

    /// <summary>A submission for one scope: a user's (userId), a team's (teamId), or the official one (neither).</summary>
    public static SubmissionEntity Submission(
        Guid scoringModelId,
        Guid evaluationId,
        Guid? teamId = null,
        Guid? userId = null,
        int moveNumber = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            ScoringModelId = scoringModelId,
            EvaluationId = evaluationId,
            TeamId = teamId,
            UserId = userId,
            MoveNumber = moveNumber,
            Status = ItemStatus.Active
        };

    public static SubmissionCategoryEntity SubmissionCategory(Guid submissionId, Guid scoringCategoryId) =>
        new() { Id = Guid.NewGuid(), SubmissionId = submissionId, ScoringCategoryId = scoringCategoryId };

    public static SubmissionOptionEntity SubmissionOption(Guid submissionCategoryId, Guid scoringOptionId, bool isSelected = false) =>
        new() { Id = Guid.NewGuid(), SubmissionCategoryId = submissionCategoryId, ScoringOptionId = scoringOptionId, IsSelected = isSelected };

    public static SubmissionCommentEntity SubmissionComment(Guid submissionOptionId, string comment = "Test Comment") =>
        new() { Id = Guid.NewGuid(), SubmissionOptionId = submissionOptionId, Comment = comment };

    public static ActionEntity Action(Guid evaluationId, Guid teamId, int moveNumber = 0, string description = "Test Action") =>
        new()
        {
            Id = Guid.NewGuid(),
            EvaluationId = evaluationId,
            TeamId = teamId,
            MoveNumber = moveNumber,
            InjectNumber = 1,
            ActionNumber = 1,
            Description = description
        };

    public static DutyEntity Duty(Guid evaluationId, Guid teamId, string name = "Test Duty") =>
        new() { Id = Guid.NewGuid(), EvaluationId = evaluationId, TeamId = teamId, Name = name };

    public static GroupEntity Group(string name = "Test Group") =>
        new() { Id = Guid.NewGuid(), Name = name, Description = name };

    public static GroupMembershipEntity GroupMembership(Guid groupId, Guid userId) =>
        new(groupId, userId) { Id = Guid.NewGuid() };

    public static DutyUserEntity DutyUser(Guid dutyId, Guid userId) =>
        new(userId, dutyId) { Id = Guid.NewGuid() };

    public static TeamMembershipEntity TeamMembership(Guid teamId, Guid userId, Guid? roleId = null) =>
        new() { Id = Guid.NewGuid(), TeamId = teamId, UserId = userId, RoleId = roleId };

    public static EvaluationMembershipEntity EvaluationMembership(Guid evaluationId, Guid? userId, Guid roleId, Guid? groupId = null) =>
        new() { Id = Guid.NewGuid(), EvaluationId = evaluationId, UserId = userId, GroupId = groupId, RoleId = roleId };

    public static ScoringModelMembershipEntity ScoringModelMembership(Guid scoringModelId, Guid? userId, Guid roleId, Guid? groupId = null) =>
        new() { Id = Guid.NewGuid(), ScoringModelId = scoringModelId, UserId = userId, GroupId = groupId, RoleId = roleId };
}
