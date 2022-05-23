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
    public class SubmissionOptionController : BaseController
    {
        private readonly ISubmissionOptionService _submissionOptionService;
        private readonly ISubmissionService _submissionService;
        private readonly IAuthorizationService _authorizationService;

        public SubmissionOptionController(ISubmissionOptionService submissionOptionService, ISubmissionService submissionService, IAuthorizationService authorizationService)
        {
            _submissionOptionService = submissionOptionService;
            _submissionService = submissionService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets SubmissionOptions
        /// </summary>
        /// <remarks>
        /// Returns a list of SubmissionOptions.
        /// </remarks>
        /// <param name="queryParameters">Result filtering criteria</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("submissionOptions")]
        [ProducesResponseType(typeof(IEnumerable<SubmissionOption>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getSubmissionOptions")]
        public async Task<IActionResult> Get([FromQuery] SubmissionOptionGet queryParameters, CancellationToken ct)
        {
            var list = await _submissionOptionService.GetAsync(queryParameters, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets SubmissionOptions for the designated ScoringCategory
        /// </summary>
        /// <remarks>
        /// Returns a list of SubmissionOptions for the ScoringCategory.
        /// </remarks>
        /// <param name="scoringCategoryId">The ID of the ScoringCategory</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("scoringCategory/{scoringCategoryId}/submissionOptions")]
        [ProducesResponseType(typeof(IEnumerable<SubmissionOption>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getSubmissionOptionsByScoringCategoryId")]
        public async Task<IActionResult> GetForScoringCategory(Guid scoringCategoryId, CancellationToken ct)
        {
            var list = await _submissionOptionService.GetForSubmissionCategoryAsync(scoringCategoryId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific SubmissionOption by id
        /// </summary>
        /// <remarks>
        /// Returns the SubmissionOption with the id specified
        /// </remarks>
        /// <param name="id">The id of the SubmissionOption</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("submissionOptions/{id}")]
        [ProducesResponseType(typeof(SubmissionOption), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getSubmissionOption")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var submissionOption = await _submissionOptionService.GetAsync(id, ct);

            if (submissionOption == null)
                throw new EntityNotFoundException<SubmissionOption>();

            return Ok(submissionOption);
        }

        /// <summary>
        /// Creates a new SubmissionOption
        /// </summary>
        /// <remarks>
        /// Creates a new SubmissionOption with the attributes specified
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="submissionOption">The data used to create the SubmissionOption</param>
        /// <param name="ct"></param>
        [HttpPost("submissionOptions")]
        [ProducesResponseType(typeof(SubmissionOption), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createSubmissionOption")]
        public async Task<IActionResult> Create([FromBody] SubmissionOption submissionOption, CancellationToken ct)
        {
            submissionOption.CreatedBy = User.GetId();
            var createdSubmissionOption = await _submissionOptionService.CreateAsync(submissionOption, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdSubmissionOption.Id }, createdSubmissionOption);
        }

        /// <summary>
        /// Updates a  SubmissionOption
        /// </summary>
        /// <remarks>
        /// Updates a SubmissionOption with the attributes specified.
        /// The ID from the route MUST MATCH the ID contained in the submissionOption parameter
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The Id of the SubmissionOption to update</param>
        /// <param name="submissionOption">The updated SubmissionOption values</param>
        /// <param name="ct"></param>
        [HttpPut("submissionOptions/{id}")]
        [ProducesResponseType(typeof(SubmissionOption), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateSubmissionOption")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] SubmissionOption submissionOption, CancellationToken ct)
        {
            submissionOption.ModifiedBy = User.GetId();
            var updatedSubmissionOption = await _submissionOptionService.UpdateAsync(id, submissionOption, ct);
            return Ok(updatedSubmissionOption);
        }

        /// <summary>
        /// Sets the selected state of a SubmissionOption to true
        /// </summary>
        /// <remarks>
        /// Sets the SubmissionOption to selected.
        /// </remarks>
        /// <param name="id">The Id of the SubmissionOption to update</param>
        /// <param name="ct"></param>
        [HttpPut("submissionOptions/{id}/select")]
        [ProducesResponseType(typeof(Submission), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "selectSubmissionOption")]
        public async Task<IActionResult> SetOptionTrue([FromRoute] Guid id, CancellationToken ct)
        {
            var updatedSubmission = await _submissionService.SetOptionAsync(id, true, ct);
            return Ok(updatedSubmission);
        }

        /// <summary>
        /// Sets the selected state of a SubmissionOption to false
        /// </summary>
        /// <remarks>
        /// Sets the SubmissionOption to not selected.
        /// </remarks>
        /// <param name="id">The Id of the SubmissionOption to update</param>
        /// <param name="ct"></param>
        [HttpPut("submissionOptions/{id}/deselect")]
        [ProducesResponseType(typeof(Submission), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "deselectSubmissionOption")]
        public async Task<IActionResult> SetOptionFalse([FromRoute] Guid id, CancellationToken ct)
        {
            var updatedSubmission = await _submissionService.SetOptionAsync(id, false, ct);
            return Ok(updatedSubmission);
        }

        /// <summary>
        /// Deletes a  SubmissionOption
        /// </summary>
        /// <remarks>
        /// Deletes a  SubmissionOption with the specified id
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The id of the SubmissionOption to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("submissionOptions/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteSubmissionOption")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _submissionOptionService.DeleteAsync(id, ct);
            return NoContent();
        }

    }
}

