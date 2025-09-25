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

public class TeamRolesController : BaseController
{
    private readonly ICiteAuthorizationService _authorizationService;
    private readonly ITeamRoleService _teamRoleService;

    public TeamRolesController(ICiteAuthorizationService authorizationService, ITeamRoleService teamRoleService)
    {
        _authorizationService = authorizationService;
        _teamRoleService = teamRoleService;
    }

    /// <summary>
    /// Get a single TeamRole.
    /// </summary>
    /// <param name="id">ID of a TeamRole.</param>
    /// <returns></returns>
    [HttpGet("team-roles/{id}")]
    [ProducesResponseType(typeof(TeamRole), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetTeamRole")]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync([SystemPermission.ViewRoles], ct))
            throw new ForbiddenException();

        var result = await _teamRoleService.GetAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get all TeamRoles.
    /// </summary>
    /// <returns></returns>
    [HttpGet("team-roles")]
    [ProducesResponseType(typeof(IEnumerable<TeamRole>), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetAllTeamRoles")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync([SystemPermission.ViewRoles], ct))
            throw new ForbiddenException();

        var result = await _teamRoleService.GetAsync(ct);
        return Ok(result);
    }
}
