// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data.Models;
using Cite.Api.Tests.Support;
using Cite.Api.ViewModels;

namespace Cite.Api.Tests.Infrastructure;

/// <summary>
/// The real AutoMapper profiles, through <see cref="TestMapper"/>, and the guard on its copy of Startup's
/// one global convention.
/// </summary>
public class MappingConfigurationTests
{
    [Fact]
    public void The_production_convention_resolver_the_test_mapper_copies_still_exists()
    {
        Assert.NotNull(typeof(Cite.Api.Startup).Assembly.GetType("Cite.Api.Infrastructure.Mapping.IgnoreNullSourceValues"));
    }

    /// <summary>The convention: a null nullable source leaves a non-nullable destination as it was.</summary>
    [Fact]
    public void A_null_nullable_source_keeps_the_non_nullable_destination()
    {
        var userId = Guid.NewGuid();
        var entity = new TeamMembershipEntity { Id = Guid.NewGuid(), TeamId = Guid.NewGuid(), UserId = userId };

        TestMapper.Mapper.Map(new TeamMembership { Id = entity.Id, TeamId = entity.TeamId, UserId = null }, entity);

        Assert.Equal(userId, entity.UserId);
    }

    [Fact]
    public void A_system_role_maps_its_permissions_both_ways()
    {
        var role = TestData.SystemRole(permissions: [Cite.Api.Data.Enumerations.SystemPermission.ViewUsers]);

        var model = TestMapper.Mapper.Map<SystemRole>(role);
        var back = TestMapper.Mapper.Map<SystemRoleEntity>(model);

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

        var model = TestMapper.Mapper.Map<Duty>(duty);

        Assert.Equal("Assignee", Assert.Single(model.Users).Name);
    }
}
