// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using AutoFixture;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Crucible.Common.Testing.Fixtures.SpecimenBuilders;

namespace Cite.Api.Tests.Shared.Fixtures;

public class CiteCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        fixture.Customizations.Add(new GuidIdBuilder());
        fixture.Customizations.Add(new DateTimeOffsetBuilder());

        RegisterEntityFactories(fixture);
    }

    private static void RegisterEntityFactories(IFixture fixture)
    {
        var now = DateTime.UtcNow;

        fixture.Register(() => new UserEntity
        {
            Id = fixture.Create<Guid>(),
            Name = $"User {fixture.Create<string>()}",
            CreatedBy = Guid.NewGuid(),
            DateCreated = now,
            UserPermissions = new List<UserPermissionEntity>(),
            TeamUsers = new List<TeamUserEntity>(),
            DutyUsers = new HashSet<DutyUserEntity>(),
            Submissions = new List<SubmissionEntity>(),
            EvaluationMemberships = new List<EvaluationMembershipEntity>(),
            TeamMemberships = new List<TeamMembershipEntity>(),
            ScoringModelMemberships = new List<ScoringModelMembershipEntity>(),
            GroupMemberships = new List<GroupMembershipEntity>()
        });

        fixture.Register(() => new TeamTypeEntity
        {
            Id = fixture.Create<Guid>(),
            Name = $"TeamType {fixture.Create<string>()}",
            CreatedBy = Guid.NewGuid(),
            DateCreated = now,
            ShowTeamTypeAverage = false,
            IsOfficialScoreContributor = false
        });

        fixture.Register(() => new ScoringModelEntity
        {
            Id = fixture.Create<Guid>(),
            Description = $"ScoringModel {fixture.Create<string>()}",
            CalculationEquation = string.Empty,
            Status = ItemStatus.Active,
            CreatedBy = Guid.NewGuid(),
            DateCreated = now,
            ScoringCategories = new HashSet<ScoringCategoryEntity>(),
            Submissions = new HashSet<SubmissionEntity>(),
            Memberships = new List<ScoringModelMembershipEntity>(),
            HideScoresOnScoreSheet = false,
            DisplayCommentTextBoxes = false,
            DisplayScoringModelByMoveNumber = false,
            ShowPastSituationDescriptions = false,
            UseSubmit = false,
            UseUserScore = false,
            UseTeamScore = false,
            UseTeamAverageScore = false,
            UseTypeAverageScore = false,
            UseOfficialScore = false,
            RightSideDisplay = RightSideDisplay.ScoreSummary
        });

        fixture.Register(() =>
        {
            var scoringModel = fixture.Create<ScoringModelEntity>();
            return new EvaluationEntity
            {
                Id = fixture.Create<Guid>(),
                Description = $"Evaluation {fixture.Create<string>()}",
                Status = ItemStatus.Active,
                CurrentMoveNumber = 0,
                SituationTime = now,
                SituationDescription = "Initial situation",
                ScoringModelId = scoringModel.Id,
                CreatedBy = Guid.NewGuid(),
                DateCreated = now,
                Teams = new HashSet<TeamEntity>(),
                Moves = new HashSet<MoveEntity>(),
                Submissions = new List<SubmissionEntity>(),
                Memberships = new List<EvaluationMembershipEntity>()
            };
        });

        fixture.Register(() =>
        {
            var evaluation = fixture.Create<EvaluationEntity>();
            var teamType = fixture.Create<TeamTypeEntity>();
            return new TeamEntity
            {
                Id = fixture.Create<Guid>(),
                Name = $"Team {fixture.Create<string>()}",
                ShortName = fixture.Create<string>()[..8],
                EvaluationId = evaluation.Id,
                TeamTypeId = teamType.Id,
                CreatedBy = Guid.NewGuid(),
                DateCreated = now,
                TeamUsers = new List<TeamUserEntity>(),
                Memberships = new List<TeamMembershipEntity>(),
                Submissions = new List<SubmissionEntity>(),
                HideScoresheet = false
            };
        });

        fixture.Register(() =>
        {
            var scoringModel = fixture.Create<ScoringModelEntity>();
            return new ScoringCategoryEntity
            {
                Id = fixture.Create<Guid>(),
                DisplayOrder = 0,
                Description = $"Category {fixture.Create<string>()}",
                CalculationEquation = string.Empty,
                IsModifierRequired = false,
                ScoringWeight = 1.0,
                MoveNumberFirstDisplay = 0,
                MoveNumberLastDisplay = 0,
                ScoringModelId = scoringModel.Id,
                CreatedBy = Guid.NewGuid(),
                DateCreated = now,
                ScoringOptions = new HashSet<ScoringOptionEntity>(),
                ScoringOptionSelection = ScoringOptionSelection.Single
            };
        });

        fixture.Register(() =>
        {
            var scoringModel = fixture.Create<ScoringModelEntity>();
            var evaluation = fixture.Create<EvaluationEntity>();
            return new SubmissionEntity
            {
                Id = fixture.Create<Guid>(),
                Score = 0.0,
                Status = ItemStatus.Active,
                ScoringModelId = scoringModel.Id,
                EvaluationId = evaluation.Id,
                MoveNumber = 0,
                CreatedBy = Guid.NewGuid(),
                DateCreated = now,
                SubmissionCategories = new HashSet<SubmissionCategoryEntity>()
            };
        });

        fixture.Register(() =>
        {
            var evaluation = fixture.Create<EvaluationEntity>();
            return new MoveEntity
            {
                Id = fixture.Create<Guid>(),
                Description = $"Move {fixture.Create<string>()}",
                MoveNumber = fixture.Create<int>() % 100,
                SituationTime = now,
                SituationDescription = "Move situation",
                EvaluationId = evaluation.Id,
                CreatedBy = Guid.NewGuid(),
                DateCreated = now
            };
        });

        fixture.Register(() =>
        {
            var evaluation = fixture.Create<EvaluationEntity>();
            var team = fixture.Create<TeamEntity>();
            return new ActionEntity
            {
                Id = fixture.Create<Guid>(),
                EvaluationId = evaluation.Id,
                TeamId = team.Id,
                MoveNumber = 0,
                InjectNumber = 0,
                ActionNumber = 0,
                Description = $"Action {fixture.Create<string>()}",
                IsChecked = false,
                CreatedBy = Guid.NewGuid(),
                DateCreated = now
            };
        });

        fixture.Register(() =>
        {
            var evaluation = fixture.Create<EvaluationEntity>();
            var team = fixture.Create<TeamEntity>();
            return new DutyEntity
            {
                Id = fixture.Create<Guid>(),
                EvaluationId = evaluation.Id,
                TeamId = team.Id,
                Name = $"Duty {fixture.Create<string>()}",
                CreatedBy = Guid.NewGuid(),
                DateCreated = now,
                DutyUsers = new HashSet<DutyUserEntity>()
            };
        });

        fixture.Register(() => new GroupEntity
        {
            Id = fixture.Create<Guid>(),
            Name = $"Group {fixture.Create<string>()}",
            Description = $"Description {fixture.Create<string>()}",
            Memberships = new List<GroupMembershipEntity>(),
            ScoringModelMemberships = new List<ScoringModelMembershipEntity>(),
            EvaluationMemberships = new List<EvaluationMembershipEntity>()
        });

        fixture.Register(() =>
        {
            var scoringCategory = fixture.Create<ScoringCategoryEntity>();
            return new ScoringOptionEntity
            {
                Id = fixture.Create<Guid>(),
                DisplayOrder = 0,
                Description = $"Option {fixture.Create<string>()}",
                IsModifier = false,
                Value = 1.0,
                ScoringCategoryId = scoringCategory.Id,
                CreatedBy = Guid.NewGuid(),
                DateCreated = now
            };
        });
    }
}
