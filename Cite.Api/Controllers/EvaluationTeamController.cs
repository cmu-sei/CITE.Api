// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Services;
using Cite.Api.ViewModels;
using Swashbuckle.AspNetCore.Annotations;

namespace Cite.Api.Controllers
{
    public class EvaluationTeamController : BaseController
    {
        private readonly IEvaluationTeamService _evaluationTeamService;
        private readonly IAuthorizationService _authorizationService;

        public EvaluationTeamController(IEvaluationTeamService evaluationTeamService, IAuthorizationService authorizationService)
        {
            _evaluationTeamService = evaluationTeamService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets all EvaluationTeams in the system
        /// </summary>
        /// <remarks>
        /// Returns a list of all of the EvaluationTeams in the system.
        /// <para />
        /// Only accessible to a SuperTeam
        /// </remarks>
        /// <returns></returns>
        [HttpGet("evaluationteams")]
        [ProducesResponseType(typeof(IEnumerable<EvaluationTeam>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getEvaluationTeams")]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var list = await _evaluationTeamService.GetAsync(ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific EvaluationTeam by id
        /// </summary>
        /// <remarks>
        /// Returns the EvaluationTeam with the id specified
        /// <para />
        /// Only accessible to a SuperTeam
        /// </remarks>
        /// <param name="id">The id of the EvaluationTeam</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("evaluationteams/{id}")]
        [ProducesResponseType(typeof(EvaluationTeam), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getEvaluationTeam")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var evaluation = await _evaluationTeamService.GetAsync(id, ct);

            if (evaluation == null)
                throw new EntityNotFoundException<EvaluationTeam>();

            return Ok(evaluation);
        }

        /// <summary>
        /// Creates a new EvaluationTeam
        /// </summary>
        /// <remarks>
        /// Creates a new EvaluationTeam with the attributes specified
        /// <para />
        /// Accessible only to a SuperTeam
        /// </remarks>
        /// <param name="evaluation">The data to create the EvaluationTeam with</param>
        /// <param name="ct"></param>
        [HttpPost("evaluationteams")]
        [ProducesResponseType(typeof(EvaluationTeam), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createEvaluationTeam")]
        public async Task<IActionResult> Create([FromBody] EvaluationTeam evaluation, CancellationToken ct)
        {
            evaluation.CreatedBy = User.GetId();
            var createdEvaluationTeam = await _evaluationTeamService.CreateAsync(evaluation, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdEvaluationTeam.Id }, createdEvaluationTeam);
        }

        /// <summary>
        /// Deletes a EvaluationTeam
        /// </summary>
        /// <remarks>
        /// Deletes a EvaluationTeam with the specified id
        /// <para />
        /// Accessible only to a SuperTeam
        /// </remarks>
        /// <param name="id">The id of the EvaluationTeam to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("evaluationteams/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteEvaluationTeam")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _evaluationTeamService.DeleteAsync(id, ct);
            return NoContent();
        }

        /// <summary>
        /// Deletes a EvaluationTeam by team ID and evaluation ID
        /// </summary>
        /// <remarks>
        /// Deletes a EvaluationTeam with the specified team ID and evaluation ID
        /// <para />
        /// Accessible only to a SuperTeam
        /// </remarks>
        /// <param name="teamId">ID of a team.</param>
        /// <param name="evaluationId">ID of a evaluation.</param>
        /// <param name="ct"></param>
        [HttpDelete("evaluations/{evaluationId}/teams/{teamId}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteEvaluationTeamByIds")]
        public async Task<IActionResult> Delete(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            await _evaluationTeamService.DeleteByIdsAsync(evaluationId, teamId, ct);
            return NoContent();
        }

    }
}

