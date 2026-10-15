// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

// cite.api: mirrors UserClaimsService.GetPermissionClaims. System permissions come from User.RoleId ->
// SystemRole { AllPermissions, Permissions }; resource permissions from EvaluationMembership,
// ScoringModelMembership (each with a role that may grant all, held directly or through a group the user
// is in, InGroup) and TeamMembership (a nullable role), one claim per resource. A user on a team of an
// evaluation is also treated as a participant by several services, which read team_memberships directly,
// so near misses use the OnNew steps, which mint a resource of their own.

using Cite.Api.Data;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;

namespace Cite.Api.Tests.Support;

/// <summary>A seeded user, and the ids of the resources seeded for them.</summary>
public sealed class TestActor
{
    public required Guid Id { get; init; }

    /// <summary>Sent as the <c>name</c> claim, which the claims service writes back to the user row.</summary>
    public required string Name { get; init; }

    /// <summary>The evaluations <see cref="TestActorBuilder.OnNewEvaluation"/> minted, in call order.</summary>
    public IReadOnlyList<Guid> NewEvaluationIds { get; init; } = [];

    /// <summary>The teams <see cref="TestActorBuilder.OnNewTeam"/> minted, in call order.</summary>
    public IReadOnlyList<Guid> NewTeamIds { get; init; } = [];

    /// <summary>The scoring models <see cref="TestActorBuilder.OnNewScoringModel"/> minted, in call order.</summary>
    public IReadOnlyList<Guid> NewScoringModelIds { get; init; } = [];
}

/// <summary>
/// Seeds a user, the role that grants their system permissions, and their memberships, so that the real
/// claims transformer derives the permissions a test needs.
/// </summary>
public sealed class TestActorBuilder(CiteContext db, CancellationToken ct)
{
    private readonly List<Func<Guid, object[]>> _memberships = [];
    private readonly List<Guid> _newEvaluations = [];
    private readonly List<Guid> _newTeams = [];
    private readonly List<Guid> _newScoringModels = [];
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Actor";
    private Guid? _roleId;
    private SystemPermission[] _systemPermissions;

    /// <summary>Fixes the actor's id, for a test that needs to know it before seeding.</summary>
    public TestActorBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public TestActorBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>Gives the actor an existing system role, such as <c>TestData.Roles.Administrator</c>.</summary>
    public TestActorBuilder WithRole(Guid roleId)
    {
        if (_systemPermissions is not null)
        {
            throw new InvalidOperationException(
                "WithRole and WithSystemPermissions both decide the actor's system role. Drop one.");
        }

        _roleId = roleId;
        return this;
    }

    /// <summary>Every system permission, by way of the seeded administrator role.</summary>
    public TestActorBuilder WithAllSystemPermissions() => WithRole(TestData.Roles.Administrator);

    /// <summary>Exactly these system permissions, by way of a role minted for this actor.</summary>
    public TestActorBuilder WithSystemPermissions(params SystemPermission[] permissions)
    {
        if (_roleId is not null)
        {
            throw new InvalidOperationException(
                "WithSystemPermissions and WithRole both decide the actor's system role. Drop one.");
        }

        _systemPermissions = permissions;
        return this;
    }

    /// <summary>
    /// A membership on an existing evaluation: either a seeded role (<c>TestData.EvaluationRoles</c>) or
    /// exactly <paramref name="permissions"/>, by way of a role minted for this membership.
    /// </summary>
    public TestActorBuilder OnEvaluation(Guid evaluationId, Guid? roleId = null, EvaluationPermission[] permissions = null)
    {
        RequireOneOf(roleId, permissions, nameof(OnEvaluation));
        var minted = permissions is null ? null : TestData.EvaluationRole(permissions);
        var role = minted?.Id ?? roleId.Value;

        _memberships.Add(userId => minted is null
            ? [TestData.EvaluationMembership(evaluationId, userId, role)]
            : [minted, TestData.EvaluationMembership(evaluationId, userId, role)]);
        return this;
    }

    /// <summary>
    /// A membership on an existing scoring model: either a seeded role (<c>TestData.ScoringModelRoles</c>)
    /// or exactly <paramref name="permissions"/>, by way of a role minted for this membership.
    /// </summary>
    public TestActorBuilder OnScoringModel(Guid scoringModelId, Guid? roleId = null, ScoringModelPermission[] permissions = null)
    {
        RequireOneOf(roleId, permissions, nameof(OnScoringModel));
        var minted = permissions is null ? null : TestData.ScoringModelRole(permissions);
        var role = minted?.Id ?? roleId.Value;

        _memberships.Add(userId => minted is null
            ? [TestData.ScoringModelMembership(scoringModelId, userId, role)]
            : [minted, TestData.ScoringModelMembership(scoringModelId, userId, role)]);
        return this;
    }

    /// <summary>
    /// A membership on an existing team: a seeded role (<c>TestData.TeamRoles</c>), exactly
    /// <paramref name="permissions"/> by way of a minted role, or neither, which leaves the membership's role
    /// null as <c>TeamMembershipService.CreateAsync</c> can.
    /// </summary>
    /// <remarks>
    /// A team membership is more than its role: services read <c>team_memberships</c> directly to decide
    /// who participates in the team's evaluation. A near miss that must not participate uses
    /// <see cref="OnNewTeam"/> under an evaluation of its own.
    /// </remarks>
    public TestActorBuilder OnTeam(Guid teamId, Guid? roleId = null, TeamPermission[] permissions = null)
    {
        if (roleId is not null && permissions is not null)
        {
            throw new InvalidOperationException($"{nameof(OnTeam)} takes a role id or permissions, not both.");
        }

        var minted = permissions is null ? null : TestData.TeamRole(permissions);

        _memberships.Add(userId => minted is null
            ? [TestData.TeamMembership(teamId, userId, roleId)]
            : [minted, TestData.TeamMembership(teamId, userId, minted.Id)]);
        return this;
    }

    /// <summary>
    /// Makes the actor a member of an existing group, which grants them the group's evaluation and scoring
    /// model memberships (<c>UserClaimsService.GetPermissionClaims</c> reads <c>group_memberships</c>).
    /// </summary>
    public TestActorBuilder InGroup(Guid groupId)
    {
        _memberships.Add(userId => [TestData.GroupMembership(groupId, userId)]);
        return this;
    }

    /// <summary>
    /// Puts the actor on a new evaluation (scored by a new scoring model) with exactly
    /// <paramref name="permissions"/>: the near miss of "the right permission on another evaluation". The
    /// minted id is on <see cref="TestActor.NewEvaluationIds"/>.
    /// </summary>
    public TestActorBuilder OnNewEvaluation(params EvaluationPermission[] permissions)
    {
        var scoringModel = TestData.ScoringModel("Near Miss Scoring Model");
        var evaluation = TestData.Evaluation(scoringModel.Id, "Near Miss Evaluation");
        var role = TestData.EvaluationRole(permissions);
        _newEvaluations.Add(evaluation.Id);

        _memberships.Add(userId => [scoringModel, evaluation, role, TestData.EvaluationMembership(evaluation.Id, userId, role.Id)]);
        return this;
    }

    /// <summary>
    /// Puts the actor on a new team of <paramref name="evaluationId"/> (which must already be saved) with
    /// exactly <paramref name="permissions"/>: the near miss of "a membership on a sibling team". The minted
    /// id is on <see cref="TestActor.NewTeamIds"/>.
    /// </summary>
    public TestActorBuilder OnNewTeam(Guid? evaluationId, params TeamPermission[] permissions)
    {
        var teamType = TestData.TeamType("Near Miss Team Type");
        var team = TestData.Team(evaluationId, teamType.Id, "Near Miss Team");
        var role = TestData.TeamRole(permissions);
        _newTeams.Add(team.Id);

        _memberships.Add(userId => [teamType, team, role, TestData.TeamMembership(team.Id, userId, role.Id)]);
        return this;
    }

    /// <summary>
    /// Puts the actor on a new scoring model with exactly <paramref name="permissions"/>: the near miss of
    /// "the right permission on another scoring model". The minted id is on
    /// <see cref="TestActor.NewScoringModelIds"/>.
    /// </summary>
    public TestActorBuilder OnNewScoringModel(params ScoringModelPermission[] permissions)
    {
        var scoringModel = TestData.ScoringModel("Near Miss Scoring Model");
        var role = TestData.ScoringModelRole(permissions);
        _newScoringModels.Add(scoringModel.Id);

        _memberships.Add(userId => [scoringModel, role, TestData.ScoringModelMembership(scoringModel.Id, userId, role.Id)]);
        return this;
    }

    /// <summary>Writes the actor and everything above to the database.</summary>
    public async Task<TestActor> SeedAsync()
    {
        var roleId = _roleId;

        if (_systemPermissions is not null)
        {
            var role = TestData.SystemRole(permissions: _systemPermissions);
            db.SystemRoles.Add(role);
            roleId = role.Id;
        }

        db.Users.Add(TestData.User(_id, _name, roleId));

        foreach (var membership in _memberships)
        {
            db.AddRange(membership(_id));
        }

        await db.SaveChangesAsync(ct);

        return new TestActor
        {
            Id = _id,
            Name = _name,
            NewEvaluationIds = [.. _newEvaluations],
            NewTeamIds = [.. _newTeams],
            NewScoringModelIds = [.. _newScoringModels]
        };
    }

    private static void RequireOneOf(Guid? roleId, Array permissions, string step)
    {
        if ((roleId is null) == (permissions is null))
        {
            throw new InvalidOperationException($"{step} takes exactly one of a role id or permissions.");
        }
    }
}
