// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Cite.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System;

namespace Cite.Api.Controllers;

public class TeamPermissionsController : BaseController
{
    private readonly ICiteAuthorizationService _authorizationService;

    public TeamPermissionsController(ICiteAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Get all SystemPermissions for the calling User.
    /// </summary>
    /// <returns></returns>
    [HttpGet("teams/{id}/me/permissions")]
    [ProducesResponseType(typeof(IEnumerable<TeamPermissionClaim>), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetMyTeamPermissions")]
    public async Task<IActionResult> GetMine(Guid id)
    {
        var result = _authorizationService.GetTeamPermissions();
        return Ok(result);
    }
}
