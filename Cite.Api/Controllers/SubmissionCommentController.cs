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
    public class SubmissionCommentController : BaseController
    {
        private readonly ISubmissionCommentService _submissionCommentService;
        private readonly ISubmissionService _submissionService;
        private readonly ICiteAuthorizationService _authorizationService;

        public SubmissionCommentController(ISubmissionCommentService submissionCommentService, ISubmissionService submissionService, ICiteAuthorizationService authorizationService)
        {
            _submissionCommentService = submissionCommentService;
            _submissionService = submissionService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets SubmissionComments for the designated SubmissionOption
        /// </summary>
        /// <remarks>
        /// Returns a list of SubmissionComments for the SubmissionOption.
        /// </remarks>
        /// <param name="submissionOptionId">The ID of the SubmissionOption</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("submissionOption/{submissionOptionId}/submissionComments")]
        [ProducesResponseType(typeof(IEnumerable<SubmissionComment>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getSubmissionCommentsBySubmissionOptionId")]
        public async Task<IActionResult> GetForSubmissionOption(Guid submissionOptionId, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<SubmissionOption>(submissionOptionId, [SystemPermission.ViewEvaluations, SystemPermission.ObserveEvaluations], [EvaluationPermission.ObserveEvaluation], ct) &&
                !await _submissionService.HasSpecificPermission<SubmissionOption>(submissionOptionId, SpecificPermission.View, ct))
                throw new ForbiddenException();

            var list = await _submissionCommentService.GetForSubmissionOptionAsync(submissionOptionId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific SubmissionComment by id
        /// </summary>
        /// <remarks>
        /// Returns the SubmissionComment with the id specified
        /// </remarks>
        /// <param name="id">The id of the SubmissionComment</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("submissionComments/{id}")]
        [ProducesResponseType(typeof(SubmissionComment), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getSubmissionComment")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<SubmissionComment>(id, [SystemPermission.ViewEvaluations, SystemPermission.ObserveEvaluations], [EvaluationPermission.ObserveEvaluation], ct) &&
                !await _submissionService.HasSpecificPermission<SubmissionComment>(id, SpecificPermission.View, ct))
                throw new ForbiddenException();

            var submissionComment = await _submissionCommentService.GetAsync(id, ct);
            if (submissionComment == null)
                throw new EntityNotFoundException<SubmissionComment>();

            return Ok(submissionComment);
        }

        /// <summary>
        /// Creates a new SubmissionComment
        /// </summary>
        /// <remarks>
        /// Creates a new SubmissionComment with the attributes specified
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="submissionComment">The data used to create the SubmissionComment</param>
        /// <param name="ct"></param>
        [HttpPost("submissionComments")]
        [ProducesResponseType(typeof(SubmissionComment), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createSubmissionComment")]
        public async Task<IActionResult> Create([FromBody] SubmissionComment submissionComment, CancellationToken ct)
        {
            if (!await _submissionService.HasSpecificPermission<SubmissionOption>(submissionComment.SubmissionOptionId, SpecificPermission.View, ct))
                throw new ForbiddenException();

            var createdSubmissionComment = await _submissionCommentService.CreateAsync(submissionComment, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdSubmissionComment.Id }, createdSubmissionComment);
        }

        /// <summary>
        /// Updates a  SubmissionComment
        /// </summary>
        /// <remarks>
        /// Updates a SubmissionComment with the attributes specified.
        /// The ID from the route MUST MATCH the ID contained in the submissionComment parameter
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The Id of the SubmissionComment to update</param>
        /// <param name="submissionComment">The updated SubmissionComment values</param>
        /// <param name="ct"></param>
        [HttpPut("submissionComments/{id}")]
        [ProducesResponseType(typeof(SubmissionComment), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateSubmissionComment")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] SubmissionComment submissionComment, CancellationToken ct)
        {
            if (!await _submissionService.HasSpecificPermission<SubmissionComment>(id, SpecificPermission.View, ct))
                throw new ForbiddenException();

            var updatedSubmissionComment = await _submissionCommentService.UpdateAsync(id, submissionComment, ct);
            return Ok(updatedSubmissionComment);
        }

        /// <summary>
        /// Deletes a  SubmissionComment
        /// </summary>
        /// <remarks>
        /// Deletes a  SubmissionComment with the specified id
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The id of the SubmissionComment to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("submissionComments/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteSubmissionComment")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            if (!await _submissionService.HasSpecificPermission<SubmissionComment>(id, SpecificPermission.View, ct))
                throw new ForbiddenException();

            await _submissionCommentService.DeleteAsync(id, ct);
            return NoContent();
        }

    }
}
