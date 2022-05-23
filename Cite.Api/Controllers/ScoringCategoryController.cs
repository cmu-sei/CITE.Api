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
    public class ScoringCategoryController : BaseController
    {
        private readonly IScoringCategoryService _scoringCategoryService;
        private readonly IAuthorizationService _authorizationService;

        public ScoringCategoryController(IScoringCategoryService scoringCategoryService, IAuthorizationService authorizationService)
        {
            _scoringCategoryService = scoringCategoryService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets ScoringCategories
        /// </summary>
        /// <remarks>
        /// Returns a list of ScoringCategories.
        /// </remarks>
        /// <param name="queryParameters">Result filtering criteria</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("scoringCategories")]
        [ProducesResponseType(typeof(IEnumerable<ScoringCategory>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getScoringCategories")]
        public async Task<IActionResult> Get([FromQuery] ScoringCategoryGet queryParameters, CancellationToken ct)
        {
            var list = await _scoringCategoryService.GetAsync(queryParameters, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets ScoringCategories for the designated ScoringModel
        /// </summary>
        /// <remarks>
        /// Returns a list of ScoringCategories for the ScoringModel.
        /// </remarks>
        /// <param name="scoringModelId">The ID of the ScoringModel</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("scoringModel/{scoringModelId}/scoringCategories")]
        [ProducesResponseType(typeof(IEnumerable<ScoringCategory>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getScoringCategoriesByScoringModelId")]
        public async Task<IActionResult> GetForScoringModel(Guid scoringModelId, CancellationToken ct)
        {
            var list = await _scoringCategoryService.GetForScoringModelAsync(scoringModelId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific ScoringCategory by id
        /// </summary>
        /// <remarks>
        /// Returns the ScoringCategory with the id specified
        /// </remarks>
        /// <param name="id">The id of the ScoringCategory</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("scoringCategories/{id}")]
        [ProducesResponseType(typeof(ScoringCategory), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getScoringCategory")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var scoringCategory = await _scoringCategoryService.GetAsync(id, ct);

            if (scoringCategory == null)
                throw new EntityNotFoundException<ScoringCategory>();

            return Ok(scoringCategory);
        }

        /// <summary>
        /// Creates a new ScoringCategory
        /// </summary>
        /// <remarks>
        /// Creates a new ScoringCategory with the attributes specified
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="scoringCategory">The data used to create the ScoringCategory</param>
        /// <param name="ct"></param>
        [HttpPost("scoringCategories")]
        [ProducesResponseType(typeof(ScoringCategory), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createScoringCategory")]
        public async Task<IActionResult> Create([FromBody] ScoringCategory scoringCategory, CancellationToken ct)
        {
            scoringCategory.CreatedBy = User.GetId();
            var createdScoringCategory = await _scoringCategoryService.CreateAsync(scoringCategory, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdScoringCategory.Id }, createdScoringCategory);
        }

        /// <summary>
        /// Updates a  ScoringCategory
        /// </summary>
        /// <remarks>
        /// Updates a ScoringCategory with the attributes specified.
        /// The ID from the route MUST MATCH the ID contained in the scoringCategory parameter
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The Id of the ScoringCategory to update</param>
        /// <param name="scoringCategory">The updated ScoringCategory values</param>
        /// <param name="ct"></param>
        [HttpPut("scoringCategories/{id}")]
        [ProducesResponseType(typeof(ScoringCategory), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateScoringCategory")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] ScoringCategory scoringCategory, CancellationToken ct)
        {
            scoringCategory.ModifiedBy = User.GetId();
            var updatedScoringCategory = await _scoringCategoryService.UpdateAsync(id, scoringCategory, ct);
            return Ok(updatedScoringCategory);
        }

        /// <summary>
        /// Deletes a  ScoringCategory
        /// </summary>
        /// <remarks>
        /// Deletes a  ScoringCategory with the specified id
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The id of the ScoringCategory to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("scoringCategories/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteScoringCategory")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _scoringCategoryService.DeleteAsync(id, ct);
            return NoContent();
        }

    }
}

