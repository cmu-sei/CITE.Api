// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Crucible.Common.EntityEvents.Events;
using Cite.Api.Data.Models;
using Cite.Api.Hubs;
using Cite.Api.Infrastructure.EventHandlers;
using Cite.Api.Tests.Support;

namespace Cite.Api.Tests.Infrastructure.EventHandlers;

/// <summary>
/// The group, evaluation and scoring model membership handlers broadcast through
/// <c>Clients.Groups(...)</c> to admin groups of their own. Each test owns its <see cref="HubRecorder{THub}"/>,
/// whose <c>ToGroups</c> reads the call with its audience.
/// </summary>
public class MembershipHandlersTests
{
    private readonly HubRecorder<MainHub> _hub = new();

    [Fact]
    public async Task GroupMembershipCreated_is_sent_to_the_admin_group_group()
    {
        var membership = TestData.GroupMembership(Guid.NewGuid(), Guid.NewGuid());

        await new GroupMembershipCreatedSignalRHandler(_hub, TestMapper.Mapper)
            .Handle(new EntityCreated<GroupMembershipEntity>(membership), TestContext.Current.CancellationToken);

        var broadcast = Assert.Single(_hub.ToGroups(MainHub.GROUP_GROUP));
        Assert.Equal(MainHubMethods.GroupMembershipCreated, broadcast.Method);
        Assert.Equal(membership.Id, Assert.IsType<Cite.Api.ViewModels.GroupMembership>(broadcast.Argument).Id);
    }

    [Fact]
    public async Task EvaluationMembershipDeleted_is_sent_to_the_admin_evaluation_group_with_the_id()
    {
        var membership = TestData.EvaluationMembership(Guid.NewGuid(), Guid.NewGuid(), TestData.EvaluationRoles.Member);

        await new EvaluationMembershipDeletedSignalRHandler(_hub)
            .Handle(new EntityDeleted<EvaluationMembershipEntity>(membership), TestContext.Current.CancellationToken);

        var broadcast = Assert.Single(_hub.ToGroups(MainHub.EVALUATION_GROUP));
        Assert.Equal((MainHubMethods.EvaluationMembershipDeleted, (object)membership.Id), (broadcast.Method, broadcast.Argument));
    }

    [Fact]
    public async Task ScoringModelMembershipCreated_is_sent_to_the_admin_scoring_model_group()
    {
        var membership = TestData.ScoringModelMembership(Guid.NewGuid(), Guid.NewGuid(), TestData.ScoringModelRoles.Editor);

        await new ScoringModelMembershipCreatedSignalRHandler(_hub, TestMapper.Mapper)
            .Handle(new EntityCreated<ScoringModelMembershipEntity>(membership), TestContext.Current.CancellationToken);

        var broadcast = Assert.Single(_hub.ToGroups(MainHub.SCORING_MODEL_GROUP));
        Assert.Equal(MainHubMethods.ScoringModelMembershipCreated, broadcast.Method);
        Assert.Equal(membership.Id, Assert.IsType<Cite.Api.ViewModels.ScoringModelMembership>(broadcast.Argument).Id);
    }
}
