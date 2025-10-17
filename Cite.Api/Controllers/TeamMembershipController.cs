// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using Cite.Api.Infrastructure.Exceptions;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Cite.Api.Data.Enumerations;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Services;
using Cite.Api.ViewModels;
using System.Threading;

namespace Cite.Api.Controllers;

public class TeamMembershipsController : BaseController
{
    private readonly ICiteAuthorizationService _authorizationService;
    private readonly ITeamMembershipService _teamMembershipService;

    public TeamMembershipsController(ICiteAuthorizationService authorizationService, ITeamMembershipService teamMembershipService)
    {
        _authorizationService = authorizationService;
        _teamMembershipService = teamMembershipService;
    }

    /// <summary>
    /// Get a single TeamMembership.
    /// </summary>
    /// <param name="id">ID of a TeamMembership.</param>
    /// <returns></returns>
    [HttpGet("teams/memberships/{id}")]
    [ProducesResponseType(typeof(TeamMembership), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetTeamMembership")]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _teamMembershipService.GetAsync(id, ct);
        if (!await _authorizationService.AuthorizeAsync<Team>(result.TeamId, [], [TeamPermission.ViewTeam], ct))
            throw new ForbiddenException();

        return Ok(result);
    }

    /// <summary>
    /// Get all TeamMemberships.
    /// </summary>
    /// <returns></returns>
    [HttpGet("teams/{id}/memberships")]
    [ProducesResponseType(typeof(IEnumerable<TeamMembership>), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetAllTeamMemberships")]
    public async Task<IActionResult> GetAll(Guid id, CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync<Team>(id, [], [TeamPermission.ViewTeam], ct))
            throw new ForbiddenException();

        var result = await _teamMembershipService.GetByTeamAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Create a new Team Membership.
    /// </summary>
    /// <param name="teamId"></param>
    /// <param name="teamMembership"></param>
    /// <returns></returns>
    [HttpPost("teams/{teamId}/memberships")]
    [ProducesResponseType(typeof(TeamMembership), (int)HttpStatusCode.Created)]
    [SwaggerOperation(OperationId = "CreateTeamMembership")]
    public async Task<IActionResult> CreateMembership([FromRoute] Guid teamId, TeamMembership teamMembership, CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync<Team>(teamMembership.TeamId, [], [TeamPermission.ManageTeam], ct))
            throw new ForbiddenException();

        var result = await _teamMembershipService.CreateAsync(teamMembership, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates a TeamMembership
    /// </summary>
    /// <remarks>
    /// Updates a TeamMembership with the attributes specified
    /// </remarks>
    /// <param name="id">The Id of the Exericse to update</param>
    /// <param name="teamMembership">The updated TeamMembership values</param>
    /// <param name="ct"></param>
    [HttpPut("Teams/Memberships/{id}")]
    [ProducesResponseType(typeof(TeamMembership), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "updateTeamMembership")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] TeamMembership teamMembership, CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync<TeamMembership>(teamMembership.TeamId, [], [TeamPermission.ManageTeam], ct))
            throw new ForbiddenException();

        var updatedTeamMembership = await _teamMembershipService.UpdateAsync(id, teamMembership, ct);
        return Ok(updatedTeamMembership);
    }

    /// <summary>
    /// Delete a Team Membership.
    /// </summary>
    /// <returns></returns>
    [HttpDelete("teams/memberships/{id}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [SwaggerOperation(OperationId = "DeleteTeamMembership")]
    public async Task<IActionResult> DeleteMembership([FromRoute] Guid id, CancellationToken ct)
    {
        var teamMembership = await _teamMembershipService.GetAsync(id, ct);
        if (!await _authorizationService.AuthorizeAsync<Team>(teamMembership.TeamId, [], [TeamPermission.ManageTeam], ct))
            throw new ForbiddenException();

        await _teamMembershipService.DeleteAsync(id, ct);
        return NoContent();
    }


}
