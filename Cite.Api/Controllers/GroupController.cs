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
    public class GroupController : BaseController
    {
        private readonly IGroupService _groupService;
        private readonly IAuthorizationService _authorizationService;

        public GroupController(IGroupService groupService, IAuthorizationService authorizationService)
        {
            _groupService = groupService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets all Group in the system
        /// </summary>
        /// <remarks>
        /// Returns a list of all of the Groups in the system.
        /// <para />
        /// Only accessible to a SuperUser
        /// </remarks>
        /// <returns></returns>
        [HttpGet("groups")]
        [ProducesResponseType(typeof(IEnumerable<Group>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getGroups")]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var list = await _groupService.GetAsync(ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets Groups for the current user
        /// </summary>
        /// <remarks>
        /// Returns a list of the current user's Groups.
        /// </remarks>
        /// <returns></returns>
        [HttpGet("groups/mine")]
        [ProducesResponseType(typeof(IEnumerable<Group>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getMyGroups")]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var list = await _groupService.GetMineAsync(ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific Group by id
        /// </summary>
        /// <remarks>
        /// Returns the Group with the id specified
        /// <para />
        /// Accessible to a SuperUser or a User that is a member of a Group within the specified Group
        /// </remarks>
        /// <param name="id">The id of the Group</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("groups/{id}")]
        [ProducesResponseType(typeof(Group), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getGroup")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var group = await _groupService.GetAsync(id, ct);

            if (group == null)
                throw new EntityNotFoundException<Group>();

            return Ok(group);
        }

        /// <summary>
        /// Creates a new Group
        /// </summary>
        /// <remarks>
        /// Creates a new Group with the attributes specified
        /// <para />
        /// Accessible only to a SuperUser or an Administrator
        /// </remarks>
        /// <param name="group">The data to create the Group with</param>
        /// <param name="ct"></param>
        [HttpPost("groups")]
        [ProducesResponseType(typeof(Group), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createGroup")]
        public async Task<IActionResult> Create([FromBody] Group group, CancellationToken ct)
        {
            group.CreatedBy = User.GetId();
            var createdGroup = await _groupService.CreateAsync(group, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdGroup.Id }, createdGroup);
        }

        /// <summary>
        /// Updates a Group
        /// </summary>
        /// <remarks>
        /// Updates a Group with the attributes specified
        /// <para />
        /// Accessible only to a SuperUser or a User on an Admin Group within the specified Group
        /// </remarks>
        /// <param name="id">The Id of the Exericse to update</param>
        /// <param name="group">The updated Group values</param>
        /// <param name="ct"></param>
        [HttpPut("groups/{id}")]
        [ProducesResponseType(typeof(Group), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateGroup")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] Group group, CancellationToken ct)
        {
            group.ModifiedBy = User.GetId();
            var updatedGroup = await _groupService.UpdateAsync(id, group, ct);
            return Ok(updatedGroup);
        }

        /// <summary>
        /// Deletes a Group
        /// </summary>
        /// <remarks>
        /// Deletes a Group with the specified id
        /// <para />
        /// Accessible only to a SuperUser or a User on an Admin Group within the specified Group
        /// </remarks>
        /// <param name="id">The id of the Group to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("groups/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteGroup")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _groupService.DeleteAsync(id, ct);
            return NoContent();
        }

    }
}
