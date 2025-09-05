// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
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

public class ScoringModelRolesController : BaseController
{
    private readonly ICiteAuthorizationService _authorizationService;
    private readonly IScoringModelRoleService _evaluationRoleService;

    public ScoringModelRolesController(ICiteAuthorizationService authorizationService, IScoringModelRoleService evaluationRoleService)
    {
        _authorizationService = authorizationService;
        _evaluationRoleService = evaluationRoleService;
    }

    /// <summary>
    /// Get a single ScoringModelRole.
    /// </summary>
    /// <param name="id">ID of a ScoringModelRole.</param>
    /// <returns></returns>
    [HttpGet("scoringModel-roles/{id}")]
    [ProducesResponseType(typeof(ScoringModelRole), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetScoringModelRole")]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync([SystemPermission.ViewRoles], ct))
            throw new ForbiddenException();

        var result = await _evaluationRoleService.GetAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get all ScoringModelRoles.
    /// </summary>
    /// <returns></returns>
    [HttpGet("scoringModel-roles")]
    [ProducesResponseType(typeof(IEnumerable<ScoringModelRole>), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetAllScoringModelRoles")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync([SystemPermission.ViewRoles], ct))
            throw new ForbiddenException();

        var result = await _evaluationRoleService.GetAsync(ct);
        return Ok(result);
    }
}
