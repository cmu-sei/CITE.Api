// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using Cite.Api.Infrastructure.Exceptions;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Cite.Api.Data;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Services;
using Cite.Api.ViewModels;
using System.Threading;

namespace Cite.Api.Controllers;

public class EvaluationRolesController : BaseController
{
    private readonly ICiteAuthorizationService _authorizationService;
    private readonly IEvaluationRoleService _evaluationRoleService;

    public EvaluationRolesController(ICiteAuthorizationService authorizationService, IEvaluationRoleService evaluationRoleService)
    {
        _authorizationService = authorizationService;
        _evaluationRoleService = evaluationRoleService;
    }

    /// <summary>
    /// Get a single EvaluationRole.
    /// </summary>
    /// <param name="id">ID of a EvaluationRole.</param>
    /// <returns></returns>
    [HttpGet("evaluation-roles/{id}")]
    [ProducesResponseType(typeof(EvaluationRole), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetEvaluationRole")]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync([SystemPermission.ViewRoles], ct))
            throw new ForbiddenException();

        var result = await _evaluationRoleService.GetAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get all EvaluationRoles.
    /// </summary>
    /// <returns></returns>
    [HttpGet("evaluation-roles")]
    [ProducesResponseType(typeof(IEnumerable<EvaluationRole>), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetAllEvaluationRoles")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync([SystemPermission.ViewRoles], ct))
            throw new ForbiddenException();

        var result = await _evaluationRoleService.GetAsync(ct);
        return Ok(result);
    }
}
