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
    public class RoleController : BaseController
    {
        private readonly IRoleService _roleService;
        private readonly ICiteAuthorizationService _authorizationService;

        public RoleController(IRoleService roleService, ICiteAuthorizationService authorizationService)
        {
            _roleService = roleService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets Roles for the specified evaluation
        /// </summary>
        /// <remarks>
        /// Returns a list of the Roles.
        /// <para />
        /// Accessible to a User that is a member of the specified Evaluation
        /// </remarks>
        /// <returns></returns>
        [HttpGet("evaluations/{evaluationId}/roles")]
        [ProducesResponseType(typeof(IEnumerable<ViewModels.Role>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getRolesByEvaluation")]
        public async Task<IActionResult> GetByEvaluation([FromRoute] Guid evaluationId, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(evaluationId, [SystemPermission.ViewEvaluations], [EvaluationPermission.ViewEvaluation], ct))
                throw new ForbiddenException();

            var list = await _roleService.GetByEvaluationAsync(evaluationId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets Roles for the specified evaluation team
        /// </summary>
        /// <remarks>
        /// Returns a list of the Roles.
        /// <para />
        /// Accessible to a User that is a member of the specified Team
        /// </remarks>
        /// <returns></returns>
        [HttpGet("evaluations/{evaluationId}/teams/{teamId}/roles")]
        [ProducesResponseType(typeof(IEnumerable<ViewModels.Role>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getRolesByEvaluationTeam")]
        public async Task<IActionResult> GetByEvaluationTeam([FromRoute] Guid evaluationId, [FromRoute] Guid teamId, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(evaluationId, [SystemPermission.ViewEvaluations, SystemPermission.ObserveEvaluations], [EvaluationPermission.ObserveEvaluation, EvaluationPermission.ViewEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Team>(teamId, [], [TeamPermission.ViewTeam], ct))
                throw new ForbiddenException();

            var list = await _roleService.GetByEvaluationTeamAsync(evaluationId, teamId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific Role by id
        /// </summary>
        /// <remarks>
        /// Returns the Role with the id specified
        /// <para />
        /// Accessible to a User that is a member of a Team within the specified Role
        /// </remarks>
        /// <param name="id">The id of the Role</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("roles/{id}")]
        [ProducesResponseType(typeof(ViewModels.Role), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getRole")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Role>(id, [SystemPermission.ViewEvaluations, SystemPermission.ObserveEvaluations], [EvaluationPermission.ObserveEvaluation, EvaluationPermission.ViewEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Role>(id, [], [TeamPermission.ViewTeam], ct))
                throw new ForbiddenException();

            var role = await _roleService.GetAsync(id, ct);

            return Ok(role);
        }

        /// <summary>
        /// Creates a new Role
        /// </summary>
        /// <remarks>
        /// Creates a new Role with the attributes specified
        /// <para />
        /// Accessible only to a SuperUser or an Administrator
        /// </remarks>
        /// <param name="role">The data to create the Role with</param>
        /// <param name="ct"></param>
        [HttpPost("roles")]
        [ProducesResponseType(typeof(ViewModels.Role), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createRole")]
        public async Task<IActionResult> Create([FromBody] ViewModels.Role role, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(role.EvaluationId, [SystemPermission.EditEvaluations, SystemPermission.EditEvaluations], [EvaluationPermission.EditEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Team>(role.TeamId, [], [TeamPermission.ManageTeam], ct))
                throw new ForbiddenException();

            role.CreatedBy = User.GetId();
            var createdRole = await _roleService.CreateAsync(role, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdRole.Id }, createdRole);
        }

        /// <summary>
        /// Updates a Role
        /// </summary>
        /// <remarks>
        /// Updates a Role with the attributes specified
        /// <para />
        /// Accessible only to a SuperUser or a User on an Admin Team within the specified Role
        /// </remarks>
        /// <param name="id">The Id of the Role to update</param>
        /// <param name="role">The updated Role values</param>
        /// <param name="ct"></param>
        [HttpPut("roles/{id}")]
        [ProducesResponseType(typeof(ViewModels.Role), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateRole")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] ViewModels.Role role, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(role.EvaluationId, [SystemPermission.EditEvaluations, SystemPermission.EditEvaluations], [EvaluationPermission.EditEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Team>(role.TeamId, [], [TeamPermission.ManageTeam], ct))
                throw new ForbiddenException();

            role.ModifiedBy = User.GetId();
            var updatedRole = await _roleService.UpdateAsync(id, role, ct);
            return Ok(updatedRole);
        }

        /// <summary>
        /// Adds a User to a Role
        /// </summary>
        /// <remarks>
        /// Adds the specified User to the specified Role
        /// </remarks>
        /// <param name="roleId">The Id of the Role to update</param>
        /// <param name="userId">The updated Role values</param>
        /// <param name="ct"></param>
        [HttpPut("roles/{roleId}/users/{userId}/add")]
        [ProducesResponseType(typeof(ViewModels.Role), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "addUserToRole")]
        public async Task<IActionResult> AddUserToRole([FromRoute] Guid roleId, [FromRoute] Guid userId, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Role>(roleId, [SystemPermission.EditEvaluations, SystemPermission.EditEvaluations], [EvaluationPermission.EditEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Role>(roleId, [], [TeamPermission.ManageTeam], ct))
                throw new ForbiddenException();

            var updatedRole = await _roleService.AddUserAsync(roleId, userId, ct);
            return Ok(updatedRole);
        }

        /// <summary>
        /// Removes a User to a Role
        /// </summary>
        /// <remarks>
        /// Removes the specified User to the specified Role
        /// </remarks>
        /// <param name="roleId">The Id of the Role to update</param>
        /// <param name="userId">The updated Role values</param>
        /// <param name="ct"></param>
        [HttpPut("roles/{roleId}/users/{userId}/remove")]
        [ProducesResponseType(typeof(ViewModels.Role), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "removeUserFromRole")]
        public async Task<IActionResult> RemoveUserFromRole([FromRoute] Guid roleId, [FromRoute] Guid userId, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Role>(roleId, [SystemPermission.EditEvaluations, SystemPermission.EditEvaluations], [EvaluationPermission.EditEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Role>(roleId, [], [TeamPermission.ManageTeam], ct))
                throw new ForbiddenException();

            var updatedRole = await _roleService.RemoveUserAsync(roleId, userId, ct);
            return Ok(updatedRole);
        }

        /// <summary>
        /// Deletes a Role
        /// </summary>
        /// <remarks>
        /// Deletes a Role with the specified id
        /// <para />
        /// Accessible only to a SuperUser or a User on an Admin Team within the specified Role
        /// </remarks>
        /// <param name="id">The id of the Role to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("roles/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteRole")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Role>(id, [SystemPermission.EditEvaluations, SystemPermission.EditEvaluations], [EvaluationPermission.EditEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Role>(id, [], [TeamPermission.ManageTeam], ct))
                throw new ForbiddenException();

            await _roleService.DeleteAsync(id, ct);
            return NoContent();
        }

    }
}
