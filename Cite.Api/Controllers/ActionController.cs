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
using Swashbuckle.AspNetCore.Annotations;

namespace Cite.Api.Controllers
{
    public class ActionController : BaseController
    {
        private readonly IActionService _actionService;
        private readonly IAuthorizationService _authorizationService;

        public ActionController(IActionService actionService, IAuthorizationService authorizationService)
        {
            _actionService = actionService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets Actions for the specified evaluation team
        /// for the current move
        /// </summary>
        /// <remarks>
        /// Returns a list of the Actions.
        /// <para />
        /// Accessible to a User that is a member of the specified Team
        /// </remarks>
        /// <returns></returns>
        [HttpGet("evaluations/{evaluationId}/teams/{teamId}/actions")]
        [ProducesResponseType(typeof(IEnumerable<ViewModels.Action>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getActionsByEvaluationTeam")]
        public async Task<IActionResult> GetByEvaluationTeam([FromRoute] Guid evaluationId, [FromRoute] Guid teamId, CancellationToken ct)
        {
            var list = await _actionService.GetByEvaluationTeamAsync(evaluationId, teamId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets Actions for the specified evaluation and move
        /// </summary>
        /// <remarks>
        /// Returns a list of the Actions.
        /// <para />
        /// Accessible to a Content Developer
        /// </remarks>
        /// <returns></returns>
        [HttpGet("evaluations/{evaluationId}/moves/{moveNumber}/actions")]
        [ProducesResponseType(typeof(IEnumerable<ViewModels.Action>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getActionsByEvaluationMove")]
        public async Task<IActionResult> GetByEvaluationMove([FromRoute] Guid evaluationId, [FromRoute] int moveNumber, CancellationToken ct)
        {
            var list = await _actionService.GetByEvaluationMoveAsync(evaluationId, moveNumber, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets Actions for the specified evaluation, move and team
        /// </summary>
        /// <remarks>
        /// Returns a list of the Actions.
        /// <para />
        /// Accessible to a User that is a member of the specified Team
        /// </remarks>
        /// <returns></returns>
        [HttpGet("evaluations/{evaluationId}/moves/{moveNumber}/teams/{teamId}/actions")]
        [ProducesResponseType(typeof(IEnumerable<ViewModels.Action>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getActionsByEvaluationMoveTeam")]
        public async Task<IActionResult> GetByEvaluationMoveTeam([FromRoute] Guid evaluationId, [FromRoute] int moveNumber, [FromRoute] Guid teamId, CancellationToken ct)
        {
            var list = await _actionService.GetByEvaluationMoveTeamAsync(evaluationId, moveNumber, teamId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific Action by id
        /// </summary>
        /// <remarks>
        /// Returns the Action with the id specified
        /// <para />
        /// Accessible to a User that is a member of a Team within the specified Action
        /// </remarks>
        /// <param name="id">The id of the Action</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("actions/{id}")]
        [ProducesResponseType(typeof(ViewModels.Action), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getAction")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var action = await _actionService.GetAsync(id, ct);

            if (action == null)
                throw new EntityNotFoundException<ViewModels.Action>();

            return Ok(action);
        }

        /// <summary>
        /// Creates a new Action
        /// </summary>
        /// <remarks>
        /// Creates a new Action with the attributes specified
        /// <para />
        /// Accessible only to a SuperUser or an Administrator
        /// </remarks>
        /// <param name="action">The data to create the Action with</param>
        /// <param name="ct"></param>
        [HttpPost("actions")]
        [ProducesResponseType(typeof(ViewModels.Action), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createAction")]
        public async Task<IActionResult> Create([FromBody] ViewModels.Action action, CancellationToken ct)
        {
            action.CreatedBy = User.GetId();
            var createdAction = await _actionService.CreateAsync(action, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdAction.Id }, createdAction);
        }

        /// <summary>
        /// Updates an Action
        /// </summary>
        /// <remarks>
        /// Updates an Action with the attributes specified
        /// <para />
        /// Accessible only to a SuperUser or a User on an Admin Team within the specified Action
        /// </remarks>
        /// <param name="id">The Id of the Action to update</param>
        /// <param name="action">The updated Action values</param>
        /// <param name="ct"></param>
        [HttpPut("actions/{id}")]
        [ProducesResponseType(typeof(ViewModels.Action), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateAction")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] ViewModels.Action action, CancellationToken ct)
        {
            action.ModifiedBy = User.GetId();
            var updatedAction = await _actionService.UpdateAsync(id, action, ct);
            return Ok(updatedAction);
        }

        /// <summary>
        /// Checks an Action
        /// </summary>
        /// <remarks>
        /// Checks an Action
        /// </remarks>
        /// <param name="id">The Id of the Action to update</param>
        /// <param name="ct"></param>
        [HttpPut("actions/{id}/check")]
        [ProducesResponseType(typeof(ViewModels.Action), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "checkAction")]
        public async Task<IActionResult> Check([FromRoute] Guid id, CancellationToken ct)
        {
            var updatedAction = await _actionService.SetIsCheckedAsync(id, true, ct);
            return Ok(updatedAction);
        }

        /// <summary>
        /// Unchecks an Action
        /// </summary>
        /// <remarks>
        /// Unchecks an Action
        /// </remarks>
        /// <param name="id">The Id of the Action to update</param>
        /// <param name="ct"></param>
        [HttpPut("actions/{id}/uncheck")]
        [ProducesResponseType(typeof(ViewModels.Action), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "uncheckAction")]
        public async Task<IActionResult> Uncheck([FromRoute] Guid id, CancellationToken ct)
        {
            var updatedAction = await _actionService.SetIsCheckedAsync(id, false, ct);
            return Ok(updatedAction);
        }

        /// <summary>
        /// Deletes an Action
        /// </summary>
        /// <remarks>
        /// Deletes an Action with the specified id
        /// <para />
        /// Accessible only to a SuperUser or a User on an Admin Team within the specified Action
        /// </remarks>
        /// <param name="id">The id of the Action to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("actions/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteAction")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _actionService.DeleteAsync(id, ct);
            return NoContent();
        }

    }
}
