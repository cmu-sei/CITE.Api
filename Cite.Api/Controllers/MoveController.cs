// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Cite.Api.Data.Enumerations;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.QueryParameters;
using Cite.Api.Services;
using Cite.Api.ViewModels;
using Swashbuckle.AspNetCore.Annotations;

namespace Cite.Api.Controllers
{
    public class MoveController : BaseController
    {
        private readonly IMoveService _moveService;
        private readonly ICiteAuthorizationService _authorizationService;

        public MoveController(IMoveService moveService, ICiteAuthorizationService authorizationService)
        {
            _moveService = moveService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets Moves by evaluation
        /// </summary>
        /// <remarks>
        /// Returns a list of Moves for the evaluation.
        /// </remarks>
        /// <param name="evaluationId"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("evaluations/{evaluationId}/moves")]
        [ProducesResponseType(typeof(IEnumerable<Move>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getByEvaluation")]
        public async Task<IActionResult> GetByEvaluation(Guid evaluationId, CancellationToken ct)
        {
            var hasPermission = await _authorizationService.AuthorizeAsync<Evaluation>(evaluationId, [SystemPermission.ViewEvaluations, SystemPermission.ObserveEvaluations], [EvaluationPermission.ObserveEvaluation, EvaluationPermission.ViewEvaluation], ct);
            var list = await _moveService.GetByEvaluationAsync(evaluationId, hasPermission, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific Move by id
        /// </summary>
        /// <remarks>
        /// Returns the Move with the id specified
        /// </remarks>
        /// <param name="id">The id of the Move</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("moves/{id}")]
        [ProducesResponseType(typeof(Move), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getMove")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var hasPermission = await _authorizationService.AuthorizeAsync([SystemPermission.ViewEvaluations, SystemPermission.ObserveEvaluations], ct);
            var move = await _moveService.GetAsync(id, hasPermission, ct);
            if (move == null)
                throw new EntityNotFoundException<Move>();

            return Ok(move);
        }

        /// <summary>
        /// Creates a new Move
        /// </summary>
        /// <remarks>
        /// Creates a new Move with the attributes specified
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="move">The data used to create the Move</param>
        /// <param name="ct"></param>
        [HttpPost("moves")]
        [ProducesResponseType(typeof(Move), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createMove")]
        public async Task<IActionResult> Create([FromBody] Move move, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(move.EvaluationId, [SystemPermission.EditEvaluations], [EvaluationPermission.EditEvaluation], ct))
                throw new ForbiddenException();

            move.CreatedBy = User.GetId();
            var createdMove = await _moveService.CreateAsync(move, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdMove.Id }, createdMove);
        }

        /// <summary>
        /// Updates a  Move
        /// </summary>
        /// <remarks>
        /// Updates a Move with the attributes specified.
        /// The ID from the route MUST MATCH the ID contained in the move parameter
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The Id of the Move to update</param>
        /// <param name="move">The updated Move values</param>
        /// <param name="ct"></param>
        [HttpPut("moves/{id}")]
        [ProducesResponseType(typeof(Move), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateMove")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] Move move, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(move.EvaluationId, [SystemPermission.EditEvaluations], [EvaluationPermission.EditEvaluation], ct))
                throw new ForbiddenException();

            move.ModifiedBy = User.GetId();
            var updatedMove = await _moveService.UpdateAsync(id, move, ct);
            return Ok(updatedMove);
        }

        /// <summary>
        /// Deletes a  Move
        /// </summary>
        /// <remarks>
        /// Deletes a  Move with the specified id
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The id of the Move to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("moves/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteMove")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(id, [SystemPermission.EditEvaluations], [EvaluationPermission.EditEvaluation], ct))
                throw new ForbiddenException();

            await _moveService.DeleteAsync(id, ct);
            return NoContent();
        }

    }
}
