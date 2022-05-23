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
    public class SubmissionCategoryController : BaseController
    {
        private readonly ISubmissionCategoryService _submissionCategoryService;
        private readonly IAuthorizationService _authorizationService;

        public SubmissionCategoryController(ISubmissionCategoryService submissionCategoryService, IAuthorizationService authorizationService)
        {
            _submissionCategoryService = submissionCategoryService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets SubmissionCategories
        /// </summary>
        /// <remarks>
        /// Returns a list of SubmissionCategories.
        /// </remarks>
        /// <param name="queryParameters">Result filtering criteria</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("submissionCategories")]
        [ProducesResponseType(typeof(IEnumerable<SubmissionCategory>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getSubmissionCategories")]
        public async Task<IActionResult> Get([FromQuery] SubmissionCategoryGet queryParameters, CancellationToken ct)
        {
            var list = await _submissionCategoryService.GetAsync(queryParameters, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets SubmissionCategories for the designated Submission
        /// </summary>
        /// <remarks>
        /// Returns a list of SubmissionCategories for the Submission.
        /// </remarks>
        /// <param name="submissionId">The ID of the Submission</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("submission/{submissionId}/submissionCategories")]
        [ProducesResponseType(typeof(IEnumerable<SubmissionCategory>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getSubmissionCategoriesBySubmissionId")]
        public async Task<IActionResult> GetForSubmission(Guid submissionId, CancellationToken ct)
        {
            var list = await _submissionCategoryService.GetForSubmissionAsync(submissionId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific SubmissionCategory by id
        /// </summary>
        /// <remarks>
        /// Returns the SubmissionCategory with the id specified
        /// </remarks>
        /// <param name="id">The id of the SubmissionCategory</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("submissionCategories/{id}")]
        [ProducesResponseType(typeof(SubmissionCategory), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getSubmissionCategory")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var submissionCategory = await _submissionCategoryService.GetAsync(id, ct);

            if (submissionCategory == null)
                throw new EntityNotFoundException<SubmissionCategory>();

            return Ok(submissionCategory);
        }

        /// <summary>
        /// Creates a new SubmissionCategory
        /// </summary>
        /// <remarks>
        /// Creates a new SubmissionCategory with the attributes specified
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="submissionCategory">The data used to create the SubmissionCategory</param>
        /// <param name="ct"></param>
        [HttpPost("submissionCategories")]
        [ProducesResponseType(typeof(SubmissionCategory), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createSubmissionCategory")]
        public async Task<IActionResult> Create([FromBody] SubmissionCategory submissionCategory, CancellationToken ct)
        {
            submissionCategory.CreatedBy = User.GetId();
            var createdSubmissionCategory = await _submissionCategoryService.CreateAsync(submissionCategory, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdSubmissionCategory.Id }, createdSubmissionCategory);
        }

        /// <summary>
        /// Updates a  SubmissionCategory
        /// </summary>
        /// <remarks>
        /// Updates a SubmissionCategory with the attributes specified.
        /// The ID from the route MUST MATCH the ID contained in the submissionCategory parameter
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The Id of the SubmissionCategory to update</param>
        /// <param name="submissionCategory">The updated SubmissionCategory values</param>
        /// <param name="ct"></param>
        [HttpPut("submissionCategories/{id}")]
        [ProducesResponseType(typeof(SubmissionCategory), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateSubmissionCategory")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] SubmissionCategory submissionCategory, CancellationToken ct)
        {
            submissionCategory.ModifiedBy = User.GetId();
            var updatedSubmissionCategory = await _submissionCategoryService.UpdateAsync(id, submissionCategory, ct);
            return Ok(updatedSubmissionCategory);
        }

        /// <summary>
        /// Deletes a  SubmissionCategory
        /// </summary>
        /// <remarks>
        /// Deletes a  SubmissionCategory with the specified id
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The id of the SubmissionCategory to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("submissionCategories/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteSubmissionCategory")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _submissionCategoryService.DeleteAsync(id, ct);
            return NoContent();
        }

    }
}

