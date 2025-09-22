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
using Cite.Api.Services;
using Cite.Api.ViewModels;
using Swashbuckle.AspNetCore.Annotations;

namespace Cite.Api.Controllers
{
    public class DutyController : BaseController
    {
        private readonly IDutyService _dutyService;
        private readonly ICiteAuthorizationService _authorizationService;

        public DutyController(IDutyService dutyService, ICiteAuthorizationService authorizationService)
        {
            _dutyService = dutyService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets Duties for the specified evaluation
        /// </summary>
        /// <remarks>
        /// Returns a list of the Duties.
        /// <para />
        /// Accessible to a User that is a member of the specified Evaluation
        /// </remarks>
        /// <returns></returns>
        [HttpGet("evaluations/{evaluationId}/duties")]
        [ProducesResponseType(typeof(IEnumerable<ViewModels.Duty>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getDutiesByEvaluation")]
        public async Task<IActionResult> GetByEvaluation([FromRoute] Guid evaluationId, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(evaluationId, [SystemPermission.ViewEvaluations], [EvaluationPermission.ViewEvaluation], ct))
                throw new ForbiddenException();

            var list = await _dutyService.GetByEvaluationAsync(evaluationId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets Duties for the specified evaluation team
        /// </summary>
        /// <remarks>
        /// Returns a list of the Duties.
        /// <para />
        /// Accessible to a User that is a member of the specified Team
        /// </remarks>
        /// <returns></returns>
        [HttpGet("evaluations/{evaluationId}/teams/{teamId}/duties")]
        [ProducesResponseType(typeof(IEnumerable<ViewModels.Duty>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getDutiesByEvaluationTeam")]
        public async Task<IActionResult> GetByEvaluationTeam([FromRoute] Guid evaluationId, [FromRoute] Guid teamId, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(evaluationId, [SystemPermission.ViewEvaluations, SystemPermission.ObserveEvaluations], [EvaluationPermission.ObserveEvaluation, EvaluationPermission.ViewEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Team>(teamId, [], [TeamPermission.ViewTeam], ct))
                throw new ForbiddenException();

            var list = await _dutyService.GetByEvaluationTeamAsync(evaluationId, teamId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific Duty by id
        /// </summary>
        /// <remarks>
        /// Returns the Duty with the id specified
        /// <para />
        /// Accessible to a User that is a member of a Team within the specified Duty
        /// </remarks>
        /// <param name="id">The id of the Duty</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("duties/{id}")]
        [ProducesResponseType(typeof(ViewModels.Duty), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getDuty")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Duty>(id, [SystemPermission.ViewEvaluations, SystemPermission.ObserveEvaluations], [EvaluationPermission.ObserveEvaluation, EvaluationPermission.ViewEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Duty>(id, [], [TeamPermission.ViewTeam], ct))
                throw new ForbiddenException();

            var duty = await _dutyService.GetAsync(id, ct);

            return Ok(duty);
        }

        /// <summary>
        /// Creates a new Duty
        /// </summary>
        /// <remarks>
        /// Creates a new Duty with the attributes specified
        /// <para />
        /// Accessible only to a SuperUser or an Administrator
        /// </remarks>
        /// <param name="duty">The data to create the Duty with</param>
        /// <param name="ct"></param>
        [HttpPost("duties")]
        [ProducesResponseType(typeof(ViewModels.Duty), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createDuty")]
        public async Task<IActionResult> Create([FromBody] ViewModels.Duty duty, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(duty.EvaluationId, [SystemPermission.EditEvaluations, SystemPermission.EditEvaluations], [EvaluationPermission.EditEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Team>(duty.TeamId, [], [TeamPermission.ManageTeam], ct))
                throw new ForbiddenException();

            duty.CreatedBy = User.GetId();
            var createdDuty = await _dutyService.CreateAsync(duty, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdDuty.Id }, createdDuty);
        }

        /// <summary>
        /// Updates a Duty
        /// </summary>
        /// <remarks>
        /// Updates a Duty with the attributes specified
        /// <para />
        /// Accessible only to a SuperUser or a User on an Admin Team within the specified Duty
        /// </remarks>
        /// <param name="id">The Id of the Duty to update</param>
        /// <param name="duty">The updated Duty values</param>
        /// <param name="ct"></param>
        [HttpPut("duties/{id}")]
        [ProducesResponseType(typeof(ViewModels.Duty), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateDuty")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] ViewModels.Duty duty, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(duty.EvaluationId, [SystemPermission.EditEvaluations, SystemPermission.EditEvaluations], [EvaluationPermission.EditEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Team>(duty.TeamId, [], [TeamPermission.ManageTeam], ct))
                throw new ForbiddenException();

            duty.ModifiedBy = User.GetId();
            var updatedDuty = await _dutyService.UpdateAsync(id, duty, ct);
            return Ok(updatedDuty);
        }

        /// <summary>
        /// Adds a User to a Duty
        /// </summary>
        /// <remarks>
        /// Adds the specified User to the specified Duty
        /// </remarks>
        /// <param name="dutyId">The Id of the Duty to update</param>
        /// <param name="userId">The updated Duty values</param>
        /// <param name="ct"></param>
        [HttpPut("duties/{dutyId}/users/{userId}/add")]
        [ProducesResponseType(typeof(ViewModels.Duty), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "addUserToDuty")]
        public async Task<IActionResult> AddUserToDuty([FromRoute] Guid dutyId, [FromRoute] Guid userId, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Duty>(dutyId, [SystemPermission.EditEvaluations, SystemPermission.EditEvaluations], [EvaluationPermission.EditEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Duty>(dutyId, [], [TeamPermission.ManageTeam], ct))
                throw new ForbiddenException();

            var updatedDuty = await _dutyService.AddUserAsync(dutyId, userId, ct);
            return Ok(updatedDuty);
        }

        /// <summary>
        /// Removes a User to a Duty
        /// </summary>
        /// <remarks>
        /// Removes the specified User to the specified Duty
        /// </remarks>
        /// <param name="dutyId">The Id of the Duty to update</param>
        /// <param name="userId">The updated Duty values</param>
        /// <param name="ct"></param>
        [HttpPut("duties/{dutyId}/users/{userId}/remove")]
        [ProducesResponseType(typeof(ViewModels.Duty), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "removeUserFromDuty")]
        public async Task<IActionResult> RemoveUserFromDuty([FromRoute] Guid dutyId, [FromRoute] Guid userId, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Duty>(dutyId, [SystemPermission.EditEvaluations, SystemPermission.EditEvaluations], [EvaluationPermission.EditEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Duty>(dutyId, [], [TeamPermission.ManageTeam], ct))
                throw new ForbiddenException();

            var updatedDuty = await _dutyService.RemoveUserAsync(dutyId, userId, ct);
            return Ok(updatedDuty);
        }

        /// <summary>
        /// Deletes a Duty
        /// </summary>
        /// <remarks>
        /// Deletes a Duty with the specified id
        /// <para />
        /// Accessible only to a SuperUser or a User on an Admin Team within the specified Duty
        /// </remarks>
        /// <param name="id">The id of the Duty to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("duties/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteDuty")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Duty>(id, [SystemPermission.EditEvaluations, SystemPermission.EditEvaluations], [EvaluationPermission.EditEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Duty>(id, [], [TeamPermission.ManageTeam], ct))
                throw new ForbiddenException();

            await _dutyService.DeleteAsync(id, ct);
            return NoContent();
        }

    }
}
