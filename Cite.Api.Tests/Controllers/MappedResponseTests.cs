// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Controllers;

/// <summary>
/// What the mappings between entities and view models preserve, over HTTP: which members a read fills and
/// which it leaves empty, how missing stored values are answered, what a write stores from the body (nested
/// records included), which writes clear a value the body leaves out, and what a copy carries.
/// </summary>
public class MappedResponseTests(DatabaseFixture fixture, CiteAppFactory factory) : ApiTestBase(fixture, factory)
{
    // ---- Teams ----------------------------------------------------------------------------------

    /// <summary>
    /// A single team is read with its team type and its memberships, each with its role; its submissions are
    /// never read with it.
    /// </summary>
    [Fact]
    public async Task A_team_is_read_with_its_type_and_its_members_roles_but_not_its_submissions()
    {
        var graph = await SeedTeamWithMember();

        var team = await ReadAsync<Team>(await RootClient.GetAsync($"api/teams/{graph.Team.Id}", Ct));

        Assert.Equal((graph.Team.Id, "Blue", "B", graph.Evaluation.Id, graph.TeamType.Id, true),
            (team.Id, team.Name, team.ShortName, team.EvaluationId, team.TeamTypeId, team.HideScoresheet));
        Assert.Equal((graph.TeamType.Id, "Cell", true, true),
            (team.TeamType.Id, team.TeamType.Name, team.TeamType.ShowTeamTypeAverage, team.TeamType.IsOfficialScoreContributor));
        Assert.Empty(team.Submissions);
        var membership = Assert.Single(team.Memberships);
        Assert.Equal((graph.Membership.Id, graph.Team.Id, (Guid?)graph.User.Id, TestData.TeamRoles.Owner),
            (membership.Id, membership.TeamId, membership.UserId, membership.RoleId));
        Assert.Equal("Owner", membership.Role.Name);
        Assert.Contains(TeamPermission.ManageTeam, membership.Role.Permissions);
    }

    /// <summary>An evaluation's teams are listed with their memberships, but without the memberships' roles, the team type or submissions.</summary>
    [Fact]
    public async Task An_evaluations_teams_are_listed_with_memberships_but_no_roles_types_or_submissions()
    {
        var graph = await SeedTeamWithMember();

        var team = Assert.Single(await ReadAsync<List<Team>>(await RootClient.GetAsync($"api/evaluations/{graph.Evaluation.Id}/teams", Ct)));

        Assert.Equal((graph.Team.Id, graph.TeamType.Id), (team.Id, team.TeamTypeId));
        Assert.Null(team.TeamType);
        Assert.Empty(team.Submissions);
        var membership = Assert.Single(team.Memberships);
        Assert.Equal(((Guid?)graph.User.Id, TestData.TeamRoles.Owner), (membership.UserId, membership.RoleId));
        Assert.Null(membership.Role);
    }

    /// <summary>An evaluation is read with its teams, bare, and its moves; its scoring model and submissions are not read with it.</summary>
    [Fact]
    public async Task An_evaluation_is_read_with_bare_teams_and_its_moves()
    {
        var graph = await SeedTeamWithMember();
        var move = TestData.Move(graph.Evaluation.Id, 0, "Opening");
        move.SituationDescription = "Calm";
        await Seed(move);

        var evaluation = await ReadAsync<Evaluation>(await RootClient.GetAsync($"api/evaluations/{graph.Evaluation.Id}", Ct));

        Assert.Equal((graph.Evaluation.Id, graph.ScoringModel.Id), (evaluation.Id, evaluation.ScoringModelId));
        Assert.Null(evaluation.ScoringModel);
        Assert.Empty(evaluation.Submissions);
        var team = Assert.Single(evaluation.Teams);
        Assert.Null(team.TeamType);
        Assert.Empty(team.Memberships);
        Assert.Empty(team.Submissions);
        var readMove = Assert.Single(evaluation.Moves);
        Assert.Equal((move.Id, "Opening", 0, "Calm", graph.Evaluation.Id), (readMove.Id, readMove.Description, readMove.MoveNumber, readMove.SituationDescription, readMove.EvaluationId));
    }

    // ---- Roles ------------------------------------------------------------------------------------

    /// <summary>A role stored without a permission list is answered with an empty one, for each kind of role.</summary>
    [Fact]
    public async Task A_role_stored_without_permissions_is_read_with_an_empty_list()
    {
        var teamRole = new TeamRoleEntity { Id = Guid.NewGuid(), Name = "No permissions", Permissions = null };
        var evaluationRole = new EvaluationRoleEntity { Id = Guid.NewGuid(), Name = "No permissions", Permissions = null };
        var scoringModelRole = new ScoringModelRoleEntity { Id = Guid.NewGuid(), Name = "No permissions", Permissions = null };
        var systemRole = new SystemRoleEntity { Id = Guid.NewGuid(), Name = "No permissions", Permissions = null };
        await Seed(teamRole, evaluationRole, scoringModelRole, systemRole);

        Assert.Empty((await ReadAsync<TeamRole>(await RootClient.GetAsync($"api/team-roles/{teamRole.Id}", Ct))).Permissions);
        Assert.Empty((await ReadAsync<EvaluationRole>(await RootClient.GetAsync($"api/evaluation-roles/{evaluationRole.Id}", Ct))).Permissions);
        Assert.Empty((await ReadAsync<ScoringModelRole>(await RootClient.GetAsync($"api/scoringModel-roles/{scoringModelRole.Id}", Ct))).Permissions);
        Assert.Empty((await ReadAsync<SystemRole>(await RootClient.GetAsync($"api/system-roles/{systemRole.Id}", Ct))).Permissions);
        Assert.Empty(Assert.Single(await ReadAsync<List<SystemRole>>(await RootClient.GetAsync("api/system-roles", Ct)), x => x.Id == systemRole.Id).Permissions);
    }

    /// <summary>A system role created or updated with a null permission list stores an empty one.</summary>
    [Fact]
    public async Task A_system_role_written_with_null_permissions_stores_an_empty_list()
    {
        var created = await ReadAsync<SystemRole>(await RootClient.PostAsJsonAsync("api/system-roles", new { name = "Created", permissions = (string[])null }, Ct));
        var role = TestData.SystemRole(permissions: [SystemPermission.ViewUsers]);
        await Seed(role);

        var updated = await ReadAsync<SystemRole>(await RootClient.PutAsJsonAsync($"api/system-roles/{role.Id}",
            new { id = role.Id, name = "Updated", allPermissions = true, immutable = true, permissions = (string[])null }, Ct));

        Assert.Empty(created.Permissions);
        Assert.Equal(("Updated", true, true), (updated.Name, updated.AllPermissions, updated.Immutable));
        Assert.Empty(updated.Permissions);
        await using var context = NewContext();
        Assert.Empty((await context.SystemRoles.SingleAsync(x => x.Id == created.Id, Ct)).Permissions);
        var stored = await context.SystemRoles.SingleAsync(x => x.Id == role.Id, Ct);
        Assert.Empty(stored.Permissions);
        Assert.Equal(("Updated", true, true), (stored.Name, stored.AllPermissions, stored.Immutable));
    }

    // ---- Users ------------------------------------------------------------------------------------

    /// <summary>A user's role id is read as any GUID format and answered in the plain one; no role id is no role.</summary>
    [Theory]
    [InlineData("B", true)]
    [InlineData("N", false)]
    [InlineData("D", true)]
    public async Task A_users_role_id_is_read_in_any_guid_format(string format, bool upper)
    {
        var text = TestData.Roles.Observer.ToString(format);
        var id = Guid.NewGuid();

        var created = await ReadAsync<User>(await RootClient.PostAsJsonAsync("api/users", new { id, name = "Formatted", roleId = upper ? text.ToUpperInvariant() : text }, Ct));

        Assert.Equal(TestData.Roles.Observer.ToString(), created.RoleId);
        Assert.Empty(created.Submissions);
        await using var context = NewContext();
        Assert.Equal(TestData.Roles.Observer, (await context.Users.SingleAsync(x => x.Id == id, Ct)).RoleId);
    }

    [Fact]
    public async Task A_user_created_without_a_role_id_has_no_role()
    {
        var id = Guid.NewGuid();

        var created = await ReadAsync<User>(await RootClient.PostAsJsonAsync("api/users", new { id, name = "No role" }, Ct));

        Assert.Null(created.RoleId);
        await using var context = NewContext();
        Assert.Null((await context.Users.SingleAsync(x => x.Id == id, Ct)).RoleId);
    }

    /// <summary>A role id that is not a GUID, the empty string included, fails the mapping: a 500, and nothing is stored.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task A_user_written_with_a_role_id_that_is_not_a_guid_is_a_server_error(string roleId)
    {
        var id = Guid.NewGuid();
        var user = TestData.User(name: "Kept", roleId: TestData.Roles.Observer);
        await Seed(user);

        await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.PostAsJsonAsync("api/users", new { id, name = "Refused", roleId }, Ct));
        await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.PutAsJsonAsync($"api/users/{user.Id}", new { id = user.Id, name = "Renamed", roleId }, Ct));

        await using var context = NewContext();
        Assert.False(await context.Users.AnyAsync(x => x.Id == id, Ct));
        var stored = await context.Users.SingleAsync(x => x.Id == user.Id, Ct);
        Assert.Equal(("Kept", (Guid?)TestData.Roles.Observer), (stored.Name, stored.RoleId));
    }

    /// <summary>An update is the whole user: a body without a role id takes the role away.</summary>
    [Fact]
    public async Task A_user_updated_without_a_role_id_loses_the_role()
    {
        var user = TestData.User(name: "Before", roleId: TestData.Roles.Observer);
        await Seed(user);

        var updated = await ReadAsync<User>(await RootClient.PutAsJsonAsync($"api/users/{user.Id}", new { id = user.Id, name = "After" }, Ct));

        Assert.Equal(("After", (string)null), (updated.Name, updated.RoleId));
        await using var context = NewContext();
        Assert.Null((await context.Users.SingleAsync(x => x.Id == user.Id, Ct)).RoleId);
    }

    // ---- Updates that write the body's id ---------------------------------------------------------

    /// <summary>
    /// A group, team type, system role or user update writes the body's id onto the record, so a body without
    /// one (or with another) tries to change the key: a 500, and the record is unchanged.
    /// </summary>
    [Fact]
    public async Task An_update_whose_body_has_no_id_is_a_server_error_and_changes_nothing()
    {
        var group = TestData.Group("Group");
        var teamType = TestData.TeamType("Type");
        var role = TestData.SystemRole(permissions: [SystemPermission.ViewUsers]);
        var user = TestData.User(name: "User");
        await Seed(group, teamType, role, user);

        await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.PutAsJsonAsync($"api/groups/{group.Id}", new { name = "Renamed" }, Ct));
        await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.PutAsJsonAsync($"api/teamTypes/{teamType.Id}", new { name = "Renamed" }, Ct));
        await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.PutAsJsonAsync($"api/system-roles/{role.Id}", new { name = "Renamed", permissions = Array.Empty<string>() }, Ct));
        await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.PutAsJsonAsync($"api/users/{user.Id}", new { id = Guid.NewGuid(), name = "Renamed" }, Ct));

        await using var context = NewContext();
        Assert.Equal("Group", (await context.Groups.SingleAsync(x => x.Id == group.Id, Ct)).Name);
        Assert.Equal("Type", (await context.TeamTypes.SingleAsync(x => x.Id == teamType.Id, Ct)).Name);
        Assert.Equal([SystemPermission.ViewUsers], (await context.SystemRoles.SingleAsync(x => x.Id == role.Id, Ct)).Permissions);
        Assert.Equal("User", (await context.Users.SingleAsync(x => x.Id == user.Id, Ct)).Name);
    }

    /// <summary>A group's view model has no description, so an update keeps the stored one.</summary>
    [Fact]
    public async Task A_group_update_keeps_the_description()
    {
        var group = TestData.Group("Before");
        group.Description = "Kept";
        await Seed(group);

        await AssertStatus(HttpStatusCode.OK, await RootClient.PutAsJsonAsync($"api/groups/{group.Id}", new { id = group.Id, name = "After" }, Ct));

        await using var context = NewContext();
        var stored = await context.Groups.SingleAsync(x => x.Id == group.Id, Ct);
        Assert.Equal(("After", "Kept"), (stored.Name, stored.Description));
    }

    // ---- Updates that clear what the body leaves out ----------------------------------------------

    /// <summary>An evaluation update writes every member of the request: leaving out the exhibit or the situation clears it.</summary>
    [Fact]
    public async Task An_evaluation_update_clears_the_members_the_body_leaves_out()
    {
        var model = TestData.ScoringModel();
        var evaluation = TestData.Evaluation(model.Id, "Before");
        evaluation.GalleryExhibitId = Guid.NewGuid();
        evaluation.SituationDescription = "Situation";
        evaluation.ShowAdvanceButton = true;
        await Seed(model, evaluation);

        await AssertStatus(HttpStatusCode.OK, await RootClient.PutAsJsonAsync($"api/evaluations/{evaluation.Id}",
            new { description = "After", status = "Active", currentMoveNumber = 2, situationTime = TestData.DefaultDateCreated.AddDays(1) }, Ct));

        await using var context = NewContext();
        var stored = await context.Evaluations.SingleAsync(x => x.Id == evaluation.Id, Ct);
        Assert.Equal(("After", ItemStatus.Active, 2, TestData.DefaultDateCreated.AddDays(1)),
            (stored.Description, stored.Status, stored.CurrentMoveNumber, stored.SituationTime));
        Assert.Equal(((Guid?)null, (string)null, false), (stored.GalleryExhibitId, stored.SituationDescription, stored.ShowAdvanceButton));
        Assert.Equal(model.Id, stored.ScoringModelId);
    }

    /// <summary>A scoring model update writes every member of the request but the severity levels: leaving out a string clears it.</summary>
    [Fact]
    public async Task A_scoring_model_update_clears_the_strings_the_body_leaves_out()
    {
        var model = TestData.ScoringModel("Before");
        model.RightSideHtmlBlock = "<p>html</p>";
        model.RightSideEmbeddedUrl = "https://example.com";
        model.UseSubmit = true;
        model.SeverityLevels = [new() { Name = "Calm", MinimumScore = 0, Color = "#FFFFFF" }];
        await Seed(model);

        await AssertStatus(HttpStatusCode.OK, await RootClient.PutAsJsonAsync($"api/scoringModels/{model.Id}",
            new { description = "After", status = "Active", rightSideDisplay = "HtmlBlock" }, Ct));

        await using var context = NewContext();
        var stored = await context.ScoringModels.SingleAsync(x => x.Id == model.Id, Ct);
        Assert.Equal(("After", RightSideDisplay.HtmlBlock), (stored.Description, stored.RightSideDisplay));
        Assert.Equal(((string)null, (string)null, (string)null, false),
            (stored.CalculationEquation, stored.RightSideHtmlBlock, stored.RightSideEmbeddedUrl, stored.UseSubmit));
        Assert.Equal("Calm", Assert.Single(stored.SeverityLevels).Name);
    }

    // ---- What a create stores from the body ---------------------------------------------------------

    /// <summary>
    /// A team membership names its role by id. A body that also nests a role object fails the mapping: a 500,
    /// and neither the membership nor a role is stored.
    /// </summary>
    [Fact]
    public async Task A_team_membership_created_with_a_nested_role_is_a_server_error()
    {
        var graph = await SeedTeamWithMember();
        var user = TestData.User(name: "Joiner");
        await Seed(user);
        var nestedRoleId = Guid.NewGuid();

        await AssertApiError(HttpStatusCode.InternalServerError, await RootClient.PostAsJsonAsync($"api/teams/{graph.Team.Id}/memberships", new
        {
            teamId = graph.Team.Id,
            userId = user.Id,
            roleId = TestData.TeamRoles.Owner,
            role = new { id = nestedRoleId, name = "Planted", permissions = new[] { "ManageTeam" } },
        }, Ct));

        await using var context = NewContext();
        Assert.False(await context.TeamMemberships.AnyAsync(x => x.UserId == user.Id, Ct));
        Assert.False(await context.TeamRoles.AnyAsync(x => x.Id == nestedRoleId, Ct));
    }

    /// <summary>
    /// An evaluation's create body is mapped whole, its nested scoring model and submissions included: both are
    /// stored, and the evaluation is put on the nested scoring model rather than on the copy of the one it names.
    /// </summary>
    [Fact]
    public async Task An_evaluation_created_with_a_nested_scoring_model_and_submissions_stores_them()
    {
        var template = TestData.ScoringModel("Template");
        await Seed(template);
        var id = Guid.NewGuid();
        var nestedModelId = Guid.NewGuid();
        var nestedSubmissionId = Guid.NewGuid();

        await AssertStatus(HttpStatusCode.Created, await RootClient.PostAsJsonAsync("api/evaluations", new
        {
            id,
            description = "Nested",
            status = "Active",
            scoringModelId = template.Id,
            situationTime = TestData.DefaultDateCreated,
            scoringModel = new
            {
                id = nestedModelId,
                description = "Nested Model",
                status = "Active",
                useSubmit = true,
                severityLevels = (object)null,
                scoringCategories = new[] { new { description = "Nested Category", scoringOptions = new[] { new { description = "Nested Option", value = 3.0 } } } },
            },
            submissions = new[] { new { id = nestedSubmissionId, scoringModelId = template.Id, moveNumber = 3, score = 5.0 } },
        }, Ct));

        await using var context = NewContext();
        var stored = await context.Evaluations.SingleAsync(x => x.Id == id, Ct);
        Assert.Equal(nestedModelId, stored.ScoringModelId);
        var model = await context.ScoringModels.Include(x => x.ScoringCategories).ThenInclude(x => x.ScoringOptions).SingleAsync(x => x.Id == nestedModelId, Ct);
        // the copy of the named template is the one attached to the evaluation
        Assert.Equal(("Nested Model", true, (Guid?)null), (model.Description, model.UseSubmit, model.EvaluationId));
        Assert.Empty(model.SeverityLevels);
        var category = Assert.Single(model.ScoringCategories);
        Assert.Equal("Nested Category", category.Description);
        Assert.Equal(("Nested Option", 3.0), (Assert.Single(category.ScoringOptions).Description, Assert.Single(category.ScoringOptions).Value));
        var submission = await context.Submissions.SingleAsync(x => x.Id == nestedSubmissionId, Ct);
        Assert.Equal(((Guid?)id, 3, 5.0), (submission.EvaluationId, submission.MoveNumber, submission.Score));
    }

    /// <summary>A scoring category's create body stores the options nested in it, as sent; a null list is answered as an empty one.</summary>
    [Fact]
    public async Task A_scoring_category_created_with_nested_options_stores_them()
    {
        var model = TestData.ScoringModel();
        await Seed(model);
        var id = Guid.NewGuid();
        var optionId = Guid.NewGuid();

        var created = await ReadAsync<ScoringCategory>(await RootClient.PostAsJsonAsync("api/scoringCategories", new
        {
            id,
            scoringModelId = model.Id,
            description = "Category",
            calculationEquation = "{sum}",
            scoringOptionSelection = "Single",
            moveNumberLastDisplay = 5,
            scoringOptions = new[] { new { id = optionId, description = "Nested", value = 2.0, isModifier = true, displayOrder = 4, scoringCategoryId = Guid.NewGuid() } },
        }, Ct));
        var withNull = await ReadAsync<ScoringCategory>(await RootClient.PostAsJsonAsync("api/scoringCategories", new
        {
            scoringModelId = model.Id,
            description = "No options",
            scoringOptionSelection = "Single",
            scoringOptions = (object)null,
        }, Ct));

        Assert.Equal((id, true), (created.Id, created.IsScored));
        Assert.Empty(withNull.ScoringOptions);
        await using var context = NewContext();
        var option = await context.ScoringOptions.SingleAsync(x => x.Id == optionId, Ct);
        Assert.Equal((id, "Nested", 2.0, true, 4, Guid.Empty), (option.ScoringCategoryId, option.Description, option.Value, option.IsModifier, option.DisplayOrder, option.CreatedBy));
    }

    /// <summary>A submission category's create body stores the options nested in it and their comments; a selected count is not stored.</summary>
    [Fact]
    public async Task A_submission_category_created_with_nested_options_and_comments_stores_them()
    {
        var graph = await SeedScoredSubmission();
        var id = Guid.NewGuid();

        var created = await ReadAsync<SubmissionCategory>(await RootClient.PostAsJsonAsync("api/submissionCategories", new
        {
            id,
            submissionId = graph.Submission.Id,
            scoringCategoryId = graph.Category.Id,
            submissionOptions = new[]
            {
                new { scoringOptionId = graph.Option.Id, isSelected = true, selectedCount = 4, submissionComments = new[] { new { comment = "Nested comment" } } },
            },
        }, Ct));

        var option = Assert.Single(created.SubmissionOptions);
        Assert.Equal((graph.Option.Id, true, 0), (option.ScoringOptionId, option.IsSelected, option.SelectedCount));
        Assert.Equal("Nested comment", Assert.Single(option.SubmissionComments).Comment);
        await using var context = NewContext();
        var stored = await context.SubmissionOptions.Include(x => x.SubmissionComments).SingleAsync(x => x.SubmissionCategoryId == id, Ct);
        Assert.Equal(option.Id, stored.Id);
        Assert.Equal("Nested comment", Assert.Single(stored.SubmissionComments).Comment);
    }

    /// <summary>A submission option's create body stores the comments nested in it.</summary>
    [Fact]
    public async Task A_submission_option_created_with_nested_comments_stores_them()
    {
        var graph = await SeedScoredSubmission();
        var category = TestData.SubmissionCategory(graph.Submission.Id, graph.Category.Id);
        await Seed(category);
        var id = Guid.NewGuid();

        var created = await ReadAsync<SubmissionOption>(await RootClient.PostAsJsonAsync("api/submissionOptions", new
        {
            id,
            submissionCategoryId = category.Id,
            scoringOptionId = graph.Option.Id,
            submissionComments = new[] { new { comment = "Nested comment" } },
        }, Ct));

        Assert.Equal("Nested comment", Assert.Single(created.SubmissionComments).Comment);
        await using var context = NewContext();
        Assert.Equal(id, (await context.SubmissionComments.SingleAsync(x => x.Comment == "Nested comment", Ct)).SubmissionOptionId);
    }

    // ---- Submissions ------------------------------------------------------------------------------

    /// <summary>A submission is read with its categories, options and comments; a stored one is never an average and has no group.</summary>
    [Fact]
    public async Task A_submission_is_read_with_its_categories_options_and_comments()
    {
        var graph = await SeedScoredSubmission();
        var category = TestData.SubmissionCategory(graph.Submission.Id, graph.Category.Id);
        category.Score = 2.5;
        var option = TestData.SubmissionOption(category.Id, graph.Option.Id, isSelected: true);
        var comment = TestData.SubmissionComment(option.Id, "Read back");
        await Seed(category, option, comment);

        var submission = await ReadAsync<Submission>(await RootClient.GetAsync($"api/submissions/{graph.Submission.Id}", Ct));

        Assert.Equal((graph.Model.Id, graph.Evaluation.Id, (Guid?)null, (Guid?)null, false), (submission.ScoringModelId, submission.EvaluationId, submission.TeamId, submission.GroupId, submission.ScoreIsAnAverage));
        var readCategory = Assert.Single(submission.SubmissionCategories);
        Assert.Equal((category.Id, 2.5, graph.Submission.Id, graph.Category.Id), (readCategory.Id, readCategory.Score, readCategory.SubmissionId, readCategory.ScoringCategoryId));
        var readOption = Assert.Single(readCategory.SubmissionOptions);
        Assert.Equal((option.Id, true, 0, category.Id, graph.Option.Id), (readOption.Id, readOption.IsSelected, readOption.SelectedCount, readOption.SubmissionCategoryId, readOption.ScoringOptionId));
        var readComment = Assert.Single(readOption.SubmissionComments);
        Assert.Equal((comment.Id, "Read back", option.Id), (readComment.Id, readComment.Comment, readComment.SubmissionOptionId));
    }

    // ---- Copies -----------------------------------------------------------------------------------

    /// <summary>A copied scoring model carries every member of the original, its categories' and its options', but the ids, parents and owner.</summary>
    [Fact]
    public async Task A_copied_scoring_model_carries_the_originals_members()
    {
        var model = TestData.ScoringModel("Original");
        model.CalculationEquation = "{average}";
        model.Status = ItemStatus.Pending;
        model.HideScoresOnScoreSheet = true;
        model.DisplayCommentTextBoxes = true;
        model.DisplayScoringModelByMoveNumber = true;
        model.ShowPastSituationDescriptions = true;
        model.UseSubmit = true;
        model.UseUserScore = true;
        model.UseTeamScore = true;
        model.UseTeamAverageScore = true;
        model.UseTypeAverageScore = true;
        model.UseOfficialScore = true;
        model.RightSideDisplay = RightSideDisplay.EmbeddedUrl;
        model.RightSideHtmlBlock = "<p>html</p>";
        model.RightSideEmbeddedUrl = "https://example.com";
        model.SeverityLevels = [new() { Name = "Calm", MinimumScore = 0, Color = "#FFFFFF", Description = "Quiet" }];
        var category = TestData.ScoringCategory(model.Id, "Impact", 3);
        category.CalculationEquation = "{max}";
        category.IsModifierRequired = true;
        category.ScoringWeight = 2.5;
        category.MoveNumberFirstDisplay = 1;
        category.MoveNumberLastDisplay = 4;
        category.ScoringOptionSelection = ScoringOptionSelection.Multiple;
        var option = TestData.ScoringOption(category.Id, "High", 7, 2);
        option.IsModifier = true;
        // a submission of the model is not part of the copy
        var evaluation = TestData.Evaluation(model.Id);
        var submission = TestData.Submission(model.Id, evaluation.Id, moveNumber: 9);
        await Seed(model, category, option, evaluation, submission);

        var copy = await ReadAsync<ScoringModel>(await RootClient.PostAsync($"api/scoringModels/{model.Id}/copy", null, Ct));

        await using var context = NewContext();
        var stored = await context.ScoringModels.Include(x => x.ScoringCategories).ThenInclude(x => x.ScoringOptions).SingleAsync(x => x.Id == copy.Id, Ct);
        Assert.NotEqual(model.Id, stored.Id);
        Assert.Equal(("Original *copy by Root*", "{average}", ItemStatus.Pending, (Guid?)null, Root.Id),
            (stored.Description, stored.CalculationEquation, stored.Status, stored.EvaluationId, stored.CreatedBy));
        Assert.Equal((true, true, true, true, true, true, true, true, true, true),
            (stored.HideScoresOnScoreSheet, stored.DisplayCommentTextBoxes, stored.DisplayScoringModelByMoveNumber, stored.ShowPastSituationDescriptions,
             stored.UseSubmit, stored.UseUserScore, stored.UseTeamScore, stored.UseTeamAverageScore, stored.UseTypeAverageScore, stored.UseOfficialScore));
        Assert.Equal((RightSideDisplay.EmbeddedUrl, "<p>html</p>", "https://example.com"), (stored.RightSideDisplay, stored.RightSideHtmlBlock, stored.RightSideEmbeddedUrl));
        var level = Assert.Single(stored.SeverityLevels);
        Assert.Equal(("Calm", 0.0, "#FFFFFF", "Quiet"), (level.Name, level.MinimumScore, level.Color, level.Description));
        var storedCategory = Assert.Single(stored.ScoringCategories);
        Assert.NotEqual(category.Id, storedCategory.Id);
        Assert.Equal((3, "Impact", "{max}", true, 2.5, 1, 4, ScoringOptionSelection.Multiple, Root.Id),
            (storedCategory.DisplayOrder, storedCategory.Description, storedCategory.CalculationEquation, storedCategory.IsModifierRequired, storedCategory.ScoringWeight,
             storedCategory.MoveNumberFirstDisplay, storedCategory.MoveNumberLastDisplay, storedCategory.ScoringOptionSelection, storedCategory.CreatedBy));
        var storedOption = Assert.Single(storedCategory.ScoringOptions);
        Assert.NotEqual(option.Id, storedOption.Id);
        Assert.Equal((2, "High", true, 7.0, Root.Id), (storedOption.DisplayOrder, storedOption.Description, storedOption.IsModifier, storedOption.Value, storedOption.CreatedBy));
        Assert.False(await context.Submissions.AnyAsync(x => x.ScoringModelId == stored.Id, Ct));
        Assert.Equal(("Original *copy by Root*", 1), (copy.Description, Assert.Single(copy.ScoringCategories).ScoringOptions.Count));
    }

    /// <summary>A copied evaluation carries every member of the original, its teams' and its moves', but the ids, parents and owner.</summary>
    [Fact]
    public async Task A_copied_evaluation_carries_the_originals_members()
    {
        var model = TestData.ScoringModel("Model");
        var evaluation = TestData.Evaluation(model.Id, "Original");
        evaluation.Status = ItemStatus.Pending;
        evaluation.CurrentMoveNumber = 1;
        evaluation.SituationDescription = "Situation";
        evaluation.GalleryExhibitId = Guid.NewGuid();
        evaluation.ShowAdvanceButton = true;
        var teamType = TestData.TeamType("Cell");
        var team = TestData.Team(evaluation.Id, teamType.Id, "Blue");
        team.ShortName = "B";
        team.HideScoresheet = true;
        var move = TestData.Move(evaluation.Id, 1, "Second");
        move.SituationDescription = "Move situation";
        move.SituationTime = TestData.DefaultDateCreated.AddHours(5);
        await Seed(model, evaluation, teamType, team, move);

        var copy = await ReadAsync<Evaluation>(await RootClient.PostAsync($"api/evaluations/{evaluation.Id}/copy", null, Ct));

        await using var context = NewContext();
        var stored = await context.Evaluations.Include(x => x.Teams).Include(x => x.Moves).SingleAsync(x => x.Id == copy.Id, Ct);
        Assert.NotEqual(evaluation.Id, stored.Id);
        Assert.NotEqual(model.Id, stored.ScoringModelId);
        Assert.Equal(("Original - Root", ItemStatus.Pending, 1, TestData.DefaultDateCreated, "Situation", evaluation.GalleryExhibitId, true, Root.Id),
            (stored.Description, stored.Status, stored.CurrentMoveNumber, stored.SituationTime, stored.SituationDescription, stored.GalleryExhibitId, stored.ShowAdvanceButton, stored.CreatedBy));
        var storedTeam = Assert.Single(stored.Teams);
        Assert.NotEqual(team.Id, storedTeam.Id);
        Assert.Equal(("Blue", "B", teamType.Id, true, Root.Id), (storedTeam.Name, storedTeam.ShortName, storedTeam.TeamTypeId, storedTeam.HideScoresheet, storedTeam.CreatedBy));
        var storedMove = Assert.Single(stored.Moves);
        Assert.NotEqual(move.Id, storedMove.Id);
        Assert.Equal(("Second", 1, TestData.DefaultDateCreated.AddHours(5), "Move situation", Root.Id),
            (storedMove.Description, storedMove.MoveNumber, storedMove.SituationTime, storedMove.SituationDescription, storedMove.CreatedBy));
        var copiedTeam = Assert.Single(copy.Teams);
        Assert.Equal(("Cell", storedTeam.Id), (copiedTeam.TeamType.Name, copiedTeam.Id));
        Assert.Single(copy.Moves);
    }

    /// <summary>
    /// A scoring model file is copied whole, so submissions written into it are stored with the copy, with their
    /// categories, options and comments.
    /// </summary>
    [Fact]
    public async Task A_scoring_model_file_with_submissions_stores_them_with_the_copy()
    {
        var graph = await SeedScoredSubmission();
        var fileId = Guid.NewGuid();
        var json = $$$"""
        {"$id":"1","Id":"{{{fileId}}}","Description":"Uploaded","CalculationEquation":"{sum}","Status":1,
         "ScoringCategories":{"$id":"2","$values":[]},
         "Submissions":{"$id":"3","$values":[{"$id":"4","Id":"{{{Guid.NewGuid()}}}","Score":3,"Status":{{{(int)ItemStatus.Complete}}},"ScoringModelId":"{{{fileId}}}",
           "EvaluationId":"{{{graph.Evaluation.Id}}}","MoveNumber":7,"ScoringModel":{"$ref":"1"},
           "SubmissionCategories":{"$id":"5","$values":[{"$id":"6","Score":1.5,"ScoringCategoryId":"{{{graph.Category.Id}}}",
             "SubmissionOptions":{"$id":"7","$values":[{"$id":"8","IsSelected":true,"ScoringOptionId":"{{{graph.Option.Id}}}",
               "SubmissionComments":{"$id":"9","$values":[{"$id":"10","Comment":"From the file"}]}}]}}]}}]},
         "Memberships":{"$id":"11","$values":[]},"SeverityLevels":[]}
        """;

        var uploaded = await ReadAsync<ScoringModel>(await RootClient.PostAsync("api/scoringModels/json", Upload(json), Ct));

        await using var context = NewContext();
        var submission = await context.Submissions
            .Include(x => x.SubmissionCategories).ThenInclude(x => x.SubmissionOptions).ThenInclude(x => x.SubmissionComments)
            .SingleAsync(x => x.ScoringModelId == uploaded.Id, Ct);
        Assert.Equal((7, 3.0, ItemStatus.Complete, (Guid?)graph.Evaluation.Id), (submission.MoveNumber, submission.Score, submission.Status, submission.EvaluationId));
        var category = Assert.Single(submission.SubmissionCategories);
        Assert.Equal((1.5, graph.Category.Id), (category.Score, category.ScoringCategoryId));
        var option = Assert.Single(category.SubmissionOptions);
        Assert.Equal((true, graph.Option.Id), (option.IsSelected, option.ScoringOptionId));
        Assert.Equal("From the file", Assert.Single(option.SubmissionComments).Comment);
    }

    /// <summary>
    /// What a file nests in a submission is copied with it: a user the file nests is stored as it is, and a
    /// team, as a copy of its own that the copied submission is put on.
    /// </summary>
    [Fact]
    public async Task A_scoring_model_file_stores_the_user_and_a_copy_of_the_team_nested_in_a_submission()
    {
        var graph = await SeedScoredSubmission();
        var teamType = TestData.TeamType();
        await Seed(teamType);
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var json = $$$"""
        {"$id":"1","Id":"{{{fileId}}}","Description":"Uploaded","Status":20,"ScoringCategories":{"$id":"2","$values":[]},
         "Submissions":{"$id":"3","$values":[{"$id":"4","Score":3,"Status":20,"ScoringModelId":"{{{fileId}}}","EvaluationId":"{{{graph.Evaluation.Id}}}",
           "UserId":"{{{userId}}}","User":{"$id":"5","Id":"{{{userId}}}","Name":"From the file"},
           "TeamId":"{{{teamId}}}","Team":{"$id":"6","Id":"{{{teamId}}}","Name":"Filed team","TeamTypeId":"{{{teamType.Id}}}","EvaluationId":"{{{graph.Evaluation.Id}}}"},
           "MoveNumber":5,"SubmissionCategories":{"$id":"7","$values":[]}}]},
         "Memberships":{"$id":"8","$values":[]},"SeverityLevels":[]}
        """;

        var uploaded = await ReadAsync<ScoringModel>(await RootClient.PostAsync("api/scoringModels/json", Upload(json), Ct));

        await using var context = NewContext();
        Assert.Equal("From the file", (await context.Users.SingleAsync(x => x.Id == userId, Ct)).Name);
        Assert.False(await context.Teams.AnyAsync(x => x.Id == teamId, Ct));
        var submission = await context.Submissions.Include(x => x.Team).SingleAsync(x => x.ScoringModelId == uploaded.Id, Ct);
        Assert.Equal(((Guid?)userId, "Filed team", (Guid?)graph.Evaluation.Id), (submission.UserId, submission.Team.Name, submission.Team.EvaluationId));
    }

    /// <summary>A submission a file lists twice (by reference) is copied, and stored, once.</summary>
    [Fact]
    public async Task A_scoring_model_file_listing_a_submission_twice_stores_it_once()
    {
        var graph = await SeedScoredSubmission();
        var fileId = Guid.NewGuid();
        var json = $$$"""
        {"$id":"1","Id":"{{{fileId}}}","Description":"Uploaded","Status":20,"ScoringCategories":{"$id":"2","$values":[]},
         "Submissions":{"$id":"3","$values":[{"$id":"4","Score":3,"Status":20,"ScoringModelId":"{{{fileId}}}","EvaluationId":"{{{graph.Evaluation.Id}}}",
           "MoveNumber":6,"ScoringModel":{"$ref":"1"},"SubmissionCategories":{"$id":"5","$values":[]}},{"$ref":"4"}]},
         "Memberships":{"$id":"6","$values":[]},"SeverityLevels":[]}
        """;

        var uploaded = await ReadAsync<ScoringModel>(await RootClient.PostAsync("api/scoringModels/json", Upload(json), Ct));

        await using var context = NewContext();
        Assert.Equal(6, (await context.Submissions.SingleAsync(x => x.ScoringModelId == uploaded.Id, Ct)).MoveNumber);
    }

    // ---- Helpers ----------------------------------------------------------------------------------

    private sealed record TeamGraph(ScoringModelEntity ScoringModel, EvaluationEntity Evaluation, TeamTypeEntity TeamType, TeamEntity Team, UserEntity User, TeamMembershipEntity Membership);

    /// <summary>An evaluation with one team, "Blue", of type "Cell", whose one member holds the Owner role, and a team submission.</summary>
    private async Task<TeamGraph> SeedTeamWithMember()
    {
        var model = TestData.ScoringModel();
        var evaluation = TestData.Evaluation(model.Id);
        var teamType = TestData.TeamType("Cell", isOfficialScoreContributor: true);
        teamType.ShowTeamTypeAverage = true;
        var team = TestData.Team(evaluation.Id, teamType.Id, "Blue");
        team.ShortName = "B";
        team.HideScoresheet = true;
        var user = TestData.User(name: "Member");
        var membership = TestData.TeamMembership(team.Id, user.Id, TestData.TeamRoles.Owner);
        var submission = TestData.Submission(model.Id, evaluation.Id, teamId: team.Id);
        await Seed(model, evaluation, teamType, team, user, membership, submission);

        return new TeamGraph(model, evaluation, teamType, team, user, membership);
    }

    private sealed record ScoredSubmission(ScoringModelEntity Model, EvaluationEntity Evaluation, ScoringCategoryEntity Category, ScoringOptionEntity Option, SubmissionEntity Submission);

    /// <summary>A scoring model with one category and option, an evaluation on it, and an official submission without categories.</summary>
    private async Task<ScoredSubmission> SeedScoredSubmission()
    {
        var model = TestData.ScoringModel();
        var evaluation = TestData.Evaluation(model.Id);
        var category = TestData.ScoringCategory(model.Id);
        var option = TestData.ScoringOption(category.Id);
        var submission = TestData.Submission(model.Id, evaluation.Id);
        await Seed(model, evaluation, category, option, submission);

        return new ScoredSubmission(model, evaluation, category, option, submission);
    }

    private static MultipartFormDataContent Upload(string json)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        return new MultipartFormDataContent { { content, "ToUpload", "scoring-model.json" } };
    }
}
