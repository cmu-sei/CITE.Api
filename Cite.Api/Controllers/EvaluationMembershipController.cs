// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
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

public class EvaluationMembershipsController : BaseController
{
    private readonly ICiteAuthorizationService _authorizationService;
    private readonly IEvaluationMembershipService _evaluationMembershipService;

    public EvaluationMembershipsController(ICiteAuthorizationService authorizationService, IEvaluationMembershipService evaluationMembershipService)
    {
        _authorizationService = authorizationService;
        _evaluationMembershipService = evaluationMembershipService;
    }

    /// <summary>
    /// Get a single EvaluationMembership.
    /// </summary>
    /// <param name="id">ID of a EvaluationMembership.</param>
    /// <returns></returns>
    [HttpGet("evaluations/memberships/{id}")]
    [ProducesResponseType(typeof(EvaluationMembership), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetEvaluationMembership")]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _evaluationMembershipService.GetAsync(id, ct);
        if (!await _authorizationService.AuthorizeAsync<Evaluation>(result.EvaluationId, [SystemPermission.ViewEvaluations], [EvaluationPermission.ViewEvaluation], ct))
            throw new ForbiddenException();

        return Ok(result);
    }

    /// <summary>
    /// Get all EvaluationMemberships.
    /// </summary>
    /// <returns></returns>
    [HttpGet("evaluations/{id}/memberships")]
    [ProducesResponseType(typeof(IEnumerable<EvaluationMembership>), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetAllEvaluationMemberships")]
    public async Task<IActionResult> GetAll(Guid id, CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync<Evaluation>(id, [SystemPermission.ViewEvaluations], [EvaluationPermission.ViewEvaluation], ct))
            throw new ForbiddenException();

        var result = await _evaluationMembershipService.GetByEvaluationAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Create a new Evaluation Membership.
    /// </summary>
    /// <param name="evaluationId"></param>
    /// <param name="evaluationMembership"></param>
    /// <returns></returns>
    [HttpPost("evaluations/{evaluationId}/memberships")]
    [ProducesResponseType(typeof(EvaluationMembership), (int)HttpStatusCode.Created)]
    [SwaggerOperation(OperationId = "CreateEvaluationMembership")]
    public async Task<IActionResult> CreateMembership([FromRoute] Guid evaluationId, EvaluationMembership evaluationMembership, CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync<Evaluation>(evaluationMembership.EvaluationId, [SystemPermission.ManageEvaluations], [EvaluationPermission.ManageEvaluation], ct))
            throw new ForbiddenException();

        var result = await _evaluationMembershipService.CreateAsync(evaluationMembership, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates a EvaluationMembership
    /// </summary>
    /// <remarks>
    /// Updates a EvaluationMembership with the attributes specified
    /// </remarks>
    /// <param name="id">The Id of the Exericse to update</param>
    /// <param name="evaluationMembership">The updated EvaluationMembership values</param>
    /// <param name="ct"></param>
    [HttpPut("Evaluations/Memberships/{id}")]
    [ProducesResponseType(typeof(EvaluationMembership), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "updateEvaluationMembership")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] EvaluationMembership evaluationMembership, CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync<EvaluationMembership>(evaluationMembership.EvaluationId, [SystemPermission.ManageEvaluations], [EvaluationPermission.ManageEvaluation], ct))
            throw new ForbiddenException();

        var updatedEvaluationMembership = await _evaluationMembershipService.UpdateAsync(id, evaluationMembership, ct);
        return Ok(updatedEvaluationMembership);
    }

    /// <summary>
    /// Delete a Evaluation Membership.
    /// </summary>
    /// <returns></returns>
    [HttpDelete("evaluations/memberships/{id}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [SwaggerOperation(OperationId = "DeleteEvaluationMembership")]
    public async Task<IActionResult> DeleteMembership([FromRoute] Guid id, CancellationToken ct)
    {
        var evaluationMembership = await _evaluationMembershipService.GetAsync(id, ct);
        if (!await _authorizationService.AuthorizeAsync<Evaluation>(evaluationMembership.EvaluationId, [SystemPermission.ManageEvaluations], [EvaluationPermission.ManageEvaluation], ct))
            throw new ForbiddenException();

        await _evaluationMembershipService.DeleteAsync(id, ct);
        return NoContent();
    }


}
