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
using Cite.Api.Infrastructure.QueryParameters;
using Cite.Api.Services;
using Cite.Api.ViewModels;
using Swashbuckle.AspNetCore.Annotations;

namespace Cite.Api.Controllers
{
    public class EvaluationController : BaseController
    {
        private readonly IEvaluationService _evaluationService;
        private readonly IAuthorizationService _authorizationService;

        public EvaluationController(IEvaluationService evaluationService, IAuthorizationService authorizationService)
        {
            _evaluationService = evaluationService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets Evaluations
        /// </summary>
        /// <remarks>
        /// Returns a list of Evaluations.
        /// </remarks>
        /// <param name="queryParameters">Result filtering criteria</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("evaluations")]
        [ProducesResponseType(typeof(IEnumerable<Evaluation>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getEvaluations")]
        public async Task<IActionResult> Get([FromQuery] EvaluationGet queryParameters, CancellationToken ct)
        {
            var list = await _evaluationService.GetAsync(queryParameters, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets Evaluations for the current user
        /// </summary>
        /// <remarks>
        /// Returns a list of the current user's active Evaluations.
        /// </remarks>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("my-evaluations")]
        [ProducesResponseType(typeof(IEnumerable<Evaluation>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getMyEvaluations")]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var list = await _evaluationService.GetMineAsync(ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific Evaluation by id
        /// </summary>
        /// <remarks>
        /// Returns the Evaluation with the id specified
        /// </remarks>
        /// <param name="id">The id of the Evaluation</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("evaluations/{id}")]
        [ProducesResponseType(typeof(Evaluation), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getEvaluation")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var evaluation = await _evaluationService.GetAsync(id, ct);

            if (evaluation == null)
                throw new EntityNotFoundException<Evaluation>();

            return Ok(evaluation);
        }

        /// <summary>
        /// Creates a new Evaluation
        /// </summary>
        /// <remarks>
        /// Creates a new Evaluation with the attributes specified
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="evaluation">The data used to create the Evaluation</param>
        /// <param name="ct"></param>
        [HttpPost("evaluations")]
        [ProducesResponseType(typeof(Evaluation), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createEvaluation")]
        public async Task<IActionResult> Create([FromBody] Evaluation evaluation, CancellationToken ct)
        {
            evaluation.CreatedBy = User.GetId();
            var createdEvaluation = await _evaluationService.CreateAsync(evaluation, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdEvaluation.Id }, createdEvaluation);
        }

        /// <summary>
        /// Updates an Evaluation
        /// </summary>
        /// <remarks>
        /// Updates an Evaluation with the attributes specified.
        /// The ID from the route MUST MATCH the ID contained in the evaluation parameter
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The Id of the Evaluation to update</param>
        /// <param name="evaluation">The updated Evaluation values</param>
        /// <param name="ct"></param>
        [HttpPut("evaluations/{id}")]
        [ProducesResponseType(typeof(Evaluation), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateEvaluation")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] Evaluation evaluation, CancellationToken ct)
        {
            evaluation.ModifiedBy = User.GetId();
            var updatedEvaluation = await _evaluationService.UpdateAsync(id, evaluation, ct);
            return Ok(updatedEvaluation);
        }

        /// <summary>
        /// Updates an Evaluation situation details
        /// </summary>
        /// <remarks>
        /// Updates an Evaluation with the attributes specified.
        /// The ID from the route MUST MATCH the ID contained in the evaluation parameter
        /// </remarks>
        /// <param name="id">The Id of the Evaluation to update</param>
        /// <param name="evaluationSituation">The updated Evaluation values</param>
        /// <param name="ct"></param>
        [HttpPut("evaluations/{id}/situation")]
        [ProducesResponseType(typeof(Evaluation), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateEvaluationSituation")]
        public async Task<IActionResult> UpdateSituation([FromRoute] Guid id, [FromBody] EvaluationSituation evaluationSituation, CancellationToken ct)
        {
            var updatedEvaluation = await _evaluationService.UpdateSituationAsync(id, evaluationSituation, ct);
            return Ok(updatedEvaluation);
        }

        /// <summary>
        /// Sets an Evaluation Current Move Number
        /// </summary>
        /// <remarks>
        /// Updates an Evaluation with the move number specified.
        /// </remarks>
        /// <param name="id">The Id of the Evaluation to update</param>
        /// <param name="move">The move value</param>
        /// <param name="ct"></param>
        [HttpPut("evaluations/{id}/move/{move}")]
        [ProducesResponseType(typeof(Evaluation), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "setEvaluationCurrentMove")]
        public async Task<IActionResult> SetCurrentMove([FromRoute] Guid id, int move, CancellationToken ct)
        {
            var updatedEvaluation = await _evaluationService.SetCurrentMoveAsync(id, move, ct);
            return Ok(updatedEvaluation);
        }

        /// <summary>
        /// Deletes an Evaluation
        /// </summary>
        /// <remarks>
        /// Deletes an Evaluation with the specified id
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The id of the Evaluation to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("evaluations/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteEvaluation")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _evaluationService.DeleteAsync(id, ct);
            return NoContent();
        }

    }
}

