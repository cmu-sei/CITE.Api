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
    public class TeamTypeController : BaseController
    {
        private readonly ITeamTypeService _teamTypeService;
        private readonly ICiteAuthorizationService _authorizationService;

        public TeamTypeController(ITeamTypeService teamTypeService, ICiteAuthorizationService authorizationService)
        {
            _teamTypeService = teamTypeService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets TeamTypes
        /// </summary>
        /// <remarks>
        /// Returns a list of TeamTypes.
        /// </remarks>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("teamTypes")]
        [ProducesResponseType(typeof(IEnumerable<TeamType>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getTeamTypes")]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var list = await _teamTypeService.GetAsync(ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific TeamType by id
        /// </summary>
        /// <remarks>
        /// Returns the TeamType with the id specified
        /// </remarks>
        /// <param name="id">The id of the TeamType</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("teamTypes/{id}")]
        [ProducesResponseType(typeof(TeamType), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getTeamType")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var teamType = await _teamTypeService.GetAsync(id, ct);
            if (teamType == null)
                throw new EntityNotFoundException<TeamType>();

            return Ok(teamType);
        }

        /// <summary>
        /// Creates a new TeamType
        /// </summary>
        /// <remarks>
        /// Creates a new TeamType with the attributes specified
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="teamType">The data used to create the TeamType</param>
        /// <param name="ct"></param>
        [HttpPost("teamTypes")]
        [ProducesResponseType(typeof(TeamType), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createTeamType")]
        public async Task<IActionResult> Create([FromBody] TeamType teamType, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync([SystemPermission.ManageTeamTypes], ct))
                throw new ForbiddenException();

            var createdTeamType = await _teamTypeService.CreateAsync(teamType, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdTeamType.Id }, createdTeamType);
        }

        /// <summary>
        /// Updates a  TeamType
        /// </summary>
        /// <remarks>
        /// Updates a TeamType with the attributes specified.
        /// The ID from the route MUST MATCH the ID contained in the teamType parameter
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The Id of the TeamType to update</param>
        /// <param name="teamType">The updated TeamType values</param>
        /// <param name="ct"></param>
        [HttpPut("teamTypes/{id}")]
        [ProducesResponseType(typeof(TeamType), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateTeamType")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] TeamType teamType, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync([SystemPermission.ManageTeamTypes], ct))
                throw new ForbiddenException();

            var updatedTeamType = await _teamTypeService.UpdateAsync(id, teamType, ct);
            return Ok(updatedTeamType);
        }

        /// <summary>
        /// Deletes a  TeamType
        /// </summary>
        /// <remarks>
        /// Deletes a  TeamType with the specified id
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The id of the TeamType to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("teamTypes/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteTeamType")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync([SystemPermission.ManageTeamTypes], ct))
                throw new ForbiddenException();

            await _teamTypeService.DeleteAsync(id, ct);
            return NoContent();
        }

    }
}
