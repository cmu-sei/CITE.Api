// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
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
using System.Data;

namespace Cite.Api.Controllers;

public class ScoringModelMembershipsController : BaseController
{
    private readonly ICiteAuthorizationService _authorizationService;
    private readonly IScoringModelMembershipService _scoringModelMembershipService;

    public ScoringModelMembershipsController(ICiteAuthorizationService authorizationService, IScoringModelMembershipService scoringModelMembershipService)
    {
        _authorizationService = authorizationService;
        _scoringModelMembershipService = scoringModelMembershipService;
    }

    /// <summary>
    /// Get a single ScoringModelMembership.
    /// </summary>
    /// <param name="id">ID of a ScoringModelMembership.</param>
    /// <returns></returns>
    [HttpGet("scoringModels/memberships/{id}")]
    [ProducesResponseType(typeof(ScoringModelMembership), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetScoringModelMembership")]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _scoringModelMembershipService.GetAsync(id, ct);
        if (!await _authorizationService.AuthorizeAsync<ScoringModel>(result.ScoringModelId, [SystemPermission.ViewScoringModels], [ScoringModelPermission.ViewScoringModel], ct))
            throw new ForbiddenException();

        return Ok(result);
    }

    /// <summary>
    /// Get all ScoringModelMemberships.
    /// </summary>
    /// <returns></returns>
    [HttpGet("scoringModels/{id}/memberships")]
    [ProducesResponseType(typeof(IEnumerable<ScoringModelMembership>), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetAllScoringModelMemberships")]
    public async Task<IActionResult> GetAll(Guid id, CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync<ScoringModel>(id, [SystemPermission.ViewScoringModels], [ScoringModelPermission.ViewScoringModel], ct))
            throw new ForbiddenException();

        var result = await _scoringModelMembershipService.GetByScoringModelAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Create a new ScoringModel Membership.
    /// </summary>
    /// <param name="scoringModelId"></param>
    /// <param name="scoringModelMembership"></param>
    /// <returns></returns>
    [HttpPost("scoringModels/{scoringModelId}/memberships")]
    [ProducesResponseType(typeof(ScoringModelMembership), (int)HttpStatusCode.Created)]
    [SwaggerOperation(OperationId = "CreateScoringModelMembership")]
    public async Task<IActionResult> CreateMembership([FromRoute] Guid scoringModelId, ScoringModelMembership scoringModelMembership, CancellationToken ct)
    {
        if (!await _authorizationService.AuthorizeAsync<ScoringModel>(scoringModelId, [SystemPermission.ManageScoringModels], [ScoringModelPermission.ManageScoringModel], ct))
            throw new ForbiddenException();

        if (scoringModelMembership.ScoringModelId != scoringModelId)
            throw new DataException("The ScoringModelId of the membership must match the ScoringModelId of the URL.");

        var result = await _scoringModelMembershipService.CreateAsync(scoringModelMembership, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates a ScoringModelMembership
    /// </summary>
    /// <remarks>
    /// Updates a ScoringModelMembership with the attributes specified
    /// </remarks>
    /// <param name="id">The Id of the Exericse to update</param>
    /// <param name="scoringModelMembership">The updated ScoringModelMembership values</param>
    /// <param name="ct"></param>
    [HttpPut("ScoringModels/Memberships/{id}")]
    [ProducesResponseType(typeof(ScoringModelMembership), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "updateScoringModelMembership")]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] ScoringModelMembership scoringModelMembership, CancellationToken ct)
    {
        var membership = await _scoringModelMembershipService.GetAsync(id, ct);
        if (!await _authorizationService.AuthorizeAsync<ScoringModel>(membership.ScoringModelId, [SystemPermission.ManageScoringModels], [ScoringModelPermission.ManageScoringModel], ct))
            throw new ForbiddenException();

        var updatedScoringModelMembership = await _scoringModelMembershipService.UpdateAsync(id, scoringModelMembership, ct);
        return Ok(updatedScoringModelMembership);
    }

    /// <summary>
    /// Delete a ScoringModel Membership.
    /// </summary>
    /// <returns></returns>
    [HttpDelete("scoringModels/memberships/{id}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [SwaggerOperation(OperationId = "DeleteScoringModelMembership")]
    public async Task<IActionResult> DeleteMembership([FromRoute] Guid id, CancellationToken ct)
    {
        var membership = await _scoringModelMembershipService.GetAsync(id, ct);
        if (!await _authorizationService.AuthorizeAsync<ScoringModel>(membership.ScoringModelId, [SystemPermission.ManageScoringModels], [ScoringModelPermission.ManageScoringModel], ct))
            throw new ForbiddenException();

        await _scoringModelMembershipService.DeleteAsync(id, ct);
        return NoContent();
    }


}
