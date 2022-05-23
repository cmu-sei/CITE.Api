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
    public class ScoringOptionController : BaseController
    {
        private readonly IScoringOptionService _scoringOptionService;
        private readonly IAuthorizationService _authorizationService;

        public ScoringOptionController(IScoringOptionService scoringOptionService, IAuthorizationService authorizationService)
        {
            _scoringOptionService = scoringOptionService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets ScoringOptions
        /// </summary>
        /// <remarks>
        /// Returns a list of ScoringOptions.
        /// </remarks>
        /// <param name="queryParameters">Result filtering criteria</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("scoringOptions")]
        [ProducesResponseType(typeof(IEnumerable<ScoringOption>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getScoringOptions")]
        public async Task<IActionResult> Get([FromQuery] ScoringOptionGet queryParameters, CancellationToken ct)
        {
            var list = await _scoringOptionService.GetAsync(queryParameters, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets ScoringOptions for the designated ScoringCategory
        /// </summary>
        /// <remarks>
        /// Returns a list of ScoringOptions for the ScoringCategory.
        /// </remarks>
        /// <param name="scoringCategoryId">The ID of the ScoringCategory</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("scoringCategory/{scoringCategoryId}/scoringOptions")]
        [ProducesResponseType(typeof(IEnumerable<ScoringOption>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getScoringOptionsByScoringCategoryId")]
        public async Task<IActionResult> GetForScoringCategory(Guid scoringCategoryId, CancellationToken ct)
        {
            var list = await _scoringOptionService.GetForScoringCategoryAsync(scoringCategoryId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific ScoringOption by id
        /// </summary>
        /// <remarks>
        /// Returns the ScoringOption with the id specified
        /// </remarks>
        /// <param name="id">The id of the ScoringOption</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("scoringOptions/{id}")]
        [ProducesResponseType(typeof(ScoringOption), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getScoringOption")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var scoringOption = await _scoringOptionService.GetAsync(id, ct);

            if (scoringOption == null)
                throw new EntityNotFoundException<ScoringOption>();

            return Ok(scoringOption);
        }

        /// <summary>
        /// Creates a new ScoringOption
        /// </summary>
        /// <remarks>
        /// Creates a new ScoringOption with the attributes specified
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="scoringOption">The data used to create the ScoringOption</param>
        /// <param name="ct"></param>
        [HttpPost("scoringOptions")]
        [ProducesResponseType(typeof(ScoringOption), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createScoringOption")]
        public async Task<IActionResult> Create([FromBody] ScoringOption scoringOption, CancellationToken ct)
        {
            scoringOption.CreatedBy = User.GetId();
            var createdScoringOption = await _scoringOptionService.CreateAsync(scoringOption, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdScoringOption.Id }, createdScoringOption);
        }

        /// <summary>
        /// Updates a  ScoringOption
        /// </summary>
        /// <remarks>
        /// Updates a ScoringOption with the attributes specified.
        /// The ID from the route MUST MATCH the ID contained in the scoringOption parameter
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The Id of the ScoringOption to update</param>
        /// <param name="scoringOption">The updated ScoringOption values</param>
        /// <param name="ct"></param>
        [HttpPut("scoringOptions/{id}")]
        [ProducesResponseType(typeof(ScoringOption), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateScoringOption")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] ScoringOption scoringOption, CancellationToken ct)
        {
            scoringOption.ModifiedBy = User.GetId();
            var updatedScoringOption = await _scoringOptionService.UpdateAsync(id, scoringOption, ct);
            return Ok(updatedScoringOption);
        }

        /// <summary>
        /// Deletes a  ScoringOption
        /// </summary>
        /// <remarks>
        /// Deletes a  ScoringOption with the specified id
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The id of the ScoringOption to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("scoringOptions/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteScoringOption")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _scoringOptionService.DeleteAsync(id, ct);
            return NoContent();
        }

    }
}

