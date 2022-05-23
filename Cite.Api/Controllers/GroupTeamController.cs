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
    public class GroupTeamController : BaseController
    {
        private readonly IGroupTeamService _groupTeamService;
        private readonly IAuthorizationService _authorizationService;

        public GroupTeamController(IGroupTeamService groupTeamService, IAuthorizationService authorizationService)
        {
            _groupTeamService = groupTeamService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets all GroupTeams in the system
        /// </summary>
        /// <remarks>
        /// Returns a list of all of the GroupTeams in the system.
        /// <para />
        /// Only accessible to a SuperUser
        /// </remarks>
        /// <returns></returns>
        [HttpGet("groupteams")]
        [ProducesResponseType(typeof(IEnumerable<GroupTeam>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getGroupTeams")]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var list = await _groupTeamService.GetAsync(ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific GroupTeam by id
        /// </summary>
        /// <remarks>
        /// Returns the GroupTeam with the id specified
        /// <para />
        /// Only accessible to a SuperUser
        /// </remarks>
        /// <param name="id">The id of the GroupTeam</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("groupteams/{id}")]
        [ProducesResponseType(typeof(GroupTeam), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getGroupTeam")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var team = await _groupTeamService.GetAsync(id, ct);

            if (team == null)
                throw new EntityNotFoundException<GroupTeam>();

            return Ok(team);
        }

        /// <summary>
        /// Creates a new GroupTeam
        /// </summary>
        /// <remarks>
        /// Creates a new GroupTeam with the attributes specified
        /// <para />
        /// Accessible only to a SuperUser
        /// </remarks>
        /// <param name="team">The data to create the GroupTeam with</param>
        /// <param name="ct"></param>
        [HttpPost("groupteams")]
        [ProducesResponseType(typeof(GroupTeam), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createGroupTeam")]
        public async Task<IActionResult> Create([FromBody] GroupTeam team, CancellationToken ct)
        {
            team.CreatedBy = User.GetId();
            var createdGroupTeam = await _groupTeamService.CreateAsync(team, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdGroupTeam.Id }, createdGroupTeam);
        }

        /// <summary>
        /// Deletes a GroupTeam
        /// </summary>
        /// <remarks>
        /// Deletes a GroupTeam with the specified id
        /// <para />
        /// Accessible only to a SuperUser
        /// </remarks>
        /// <param name="id">The id of the GroupTeam to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("groupteams/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteGroupTeam")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _groupTeamService.DeleteAsync(id, ct);
            return NoContent();
        }

        /// <summary>
        /// Deletes a GroupTeam by group ID and team ID
        /// </summary>
        /// <remarks>
        /// Deletes a GroupTeam with the specified group ID and team ID
        /// <para />
        /// Accessible only to a SuperUser
        /// </remarks>
        /// <param name="groupId">ID of a group.</param>
        /// <param name="teamId">ID of a team.</param>
        /// <param name="ct"></param>
        [HttpDelete("groups/{groupId}/teams/{teamId}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteGroupTeamByIds")]
        public async Task<IActionResult> Delete(Guid teamId, Guid groupId, CancellationToken ct)
        {
            await _groupTeamService.DeleteByIdsAsync(teamId, groupId, ct);
            return NoContent();
        }

    }
}

