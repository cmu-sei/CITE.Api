// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Cite.Api.Data.Enumerations;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.Identity;
using Cite.Api.Infrastructure.QueryParameters;
using Cite.Api.Services;
using Cite.Api.ViewModels;
using Swashbuckle.AspNetCore.Annotations;

namespace Cite.Api.Controllers
{
    public class SubmissionController : BaseController
    {
        private readonly ISubmissionService _submissionService;
        private readonly ICiteAuthorizationService _authorizationService;
        private readonly IIdentityResolver _identityResolver;

        public SubmissionController(ISubmissionService submissionService, ICiteAuthorizationService authorizationService)
        {
            _submissionService = submissionService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets Submissions
        /// </summary>
        /// <remarks>
        /// Returns a list of Submissions.
        /// </remarks>
        /// <param name="queryParameters">Result filtering criteria</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("submissions")]
        [ProducesResponseType(typeof(IEnumerable<Submission>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getSubmissions")]
        public async Task<IActionResult> Get([FromQuery] SubmissionGet queryParameters, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync([SystemPermission.ManageEvaluations], ct))
                throw new ForbiddenException();

            var list = (await _submissionService.GetAsync(queryParameters, ct)).ToList();

            return Ok(list);
        }

        /// <summary>
        /// Gets Submissions by evaluation
        /// </summary>
        /// <remarks>
        /// Returns a list of Submissions for the evaluation.
        /// </remarks>
        /// <param name="evaluationId"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("evaluations/{evaluationId}/submissions")]
        [ProducesResponseType(typeof(IEnumerable<Submission>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getByEvaluation")]
        public async Task<IActionResult> GetByEvaluation(Guid evaluationId, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(evaluationId, [SystemPermission.ManageEvaluations], [EvaluationPermission.ManageEvaluation], ct))
                throw new ForbiddenException();

            var list = await _submissionService.GetByEvaluationAsync(evaluationId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets Submissions by evaluation for current user
        /// </summary>
        /// <remarks>
        /// Returns a list of Submissions for the evaluation for the current user.
        /// </remarks>
        /// <param name="evaluationId"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("evaluations/{evaluationId}/my-submissions")]
        [ProducesResponseType(typeof(IEnumerable<Submission>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getMineByEvaluation")]
        public async Task<IActionResult> GetMineByEvaluation(Guid evaluationId, CancellationToken ct)
        {
            var list = await _submissionService.GetMineByEvaluationAsync(evaluationId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets Submissions by evaluation team
        /// </summary>
        /// <remarks>
        /// Returns a list of Submissions for the evaluation team specified.
        /// </remarks>
        /// <param name="evaluationId"></param>
        /// <param name="teamId"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("evaluations/{evaluationId}/teams/{teamId}/submissions")]
        [ProducesResponseType(typeof(IEnumerable<Submission>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getByEvaluationTeam")]
        public async Task<IActionResult> GetByEvaluationTeam(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(evaluationId, [SystemPermission.ObserveEvaluations], [EvaluationPermission.ObserveEvaluation], ct) &&
                !await _authorizationService.AuthorizeAsync<Team>(teamId, [], [TeamPermission.ViewTeam], ct))
                throw new ForbiddenException();

            var list = await _submissionService.GetByEvaluationTeamAsync(evaluationId, teamId, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific Submission by id
        /// </summary>
        /// <remarks>
        /// Returns the Submission with the id specified
        /// </remarks>
        /// <param name="id">The id of the Submission</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("submissions/{id}")]
        [ProducesResponseType(typeof(Submission), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getSubmission")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Submission>(id, [SystemPermission.ViewEvaluations, SystemPermission.ObserveEvaluations], [EvaluationPermission.ViewEvaluation, EvaluationPermission.ObserveEvaluation], ct) &&
                !await _submissionService.HasSpecificPermission<Submission>(id, SpecificPermission.View, ct))
                throw new ForbiddenException();

            var submission = await _submissionService.GetAsync(id, ct);
            if (submission == null)
                throw new EntityNotFoundException<Submission>();

            return Ok(submission);
        }

        /// <summary>
        /// Creates a new Submission
        /// </summary>
        /// <remarks>
        /// Creates a new Submission with the attributes specified
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="submission">The data used to create the Submission</param>
        /// <param name="ct"></param>
        [HttpPost("submissions")]
        [ProducesResponseType(typeof(Submission), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createSubmission")]
        public async Task<IActionResult> Create([FromBody] Submission submission, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(submission.EvaluationId, [SystemPermission.ManageEvaluations], [EvaluationPermission.ManageEvaluation], ct))
                throw new ForbiddenException();

            var createdSubmission = await _submissionService.CreateAsync(submission, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdSubmission.Id }, createdSubmission);
        }

        /// <summary>
        /// Updates a  Submission
        /// </summary>
        /// <remarks>
        /// Updates a Submission with the attributes specified.
        /// The ID from the route MUST MATCH the ID contained in the submission parameter
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The Id of the Submission to update</param>
        /// <param name="submission">The updated Submission values</param>
        /// <param name="ct"></param>
        [HttpPut("submissions/{id}")]
        [ProducesResponseType(typeof(Submission), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateSubmission")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] Submission submission, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Evaluation>(submission.EvaluationId, [SystemPermission.ManageEvaluations], [EvaluationPermission.ManageEvaluation], ct))
                throw new ForbiddenException();

            var updatedSubmission = await _submissionService.UpdateAsync(id, submission, ct);
            return Ok(updatedSubmission);
        }

        /// <summary>
        /// Deletes a  Submission
        /// </summary>
        /// <remarks>
        /// Deletes a  Submission with the specified id
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The id of the Submission to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("submissions/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteSubmission")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            if (!await _authorizationService.AuthorizeAsync<Submission>(id, [SystemPermission.ManageEvaluations], [EvaluationPermission.ManageEvaluation], ct))
                throw new ForbiddenException();

            await _submissionService.DeleteAsync(id, ct);
            return NoContent();
        }

        /// <summary>
        /// Clears Submission Selections
        /// </summary>
        /// <remarks>
        /// Updates a Submission to no selections.
        /// <para />
        /// </remarks>
        /// <param name="id">The Id of the Submission to update</param>
        /// <param name="ct"></param>
        [HttpPut("submissions/{id}/clear")]
        [ProducesResponseType(typeof(Submission), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "clearSubmission")]
        public async Task<IActionResult> ClearSubmission([FromRoute] Guid id, CancellationToken ct)
        {
            if (!await _submissionService.HasSpecificPermission<Submission>(id, SpecificPermission.Score, ct))
                throw new ForbiddenException();

            var clearedSubmission = await _submissionService.ClearSelectionsAsync(id, ct);
            return Ok(clearedSubmission);
        }

        /// <summary>
        /// Presets Submission Selections to previous move values
        /// </summary>
        /// <remarks>
        /// Updates a Submission to previous move submission selections.
        /// <para />
        /// </remarks>
        /// <param name="id">The Id of the Submission to update</param>
        /// <param name="ct"></param>
        [HttpPut("submissions/{id}/preset")]
        [ProducesResponseType(typeof(Submission), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "presetSubmission")]
        public async Task<IActionResult> PresetSubmission([FromRoute] Guid id, CancellationToken ct)
        {
            if (!await _submissionService.HasSpecificPermission<Submission>(id, SpecificPermission.Score, ct))
                throw new ForbiddenException();

            var updatedSubmission = await _submissionService.PresetSelectionsAsync(id, ct);
            return Ok(updatedSubmission);
        }

        /// <summary>
        /// Adds a new SubmissionComment
        /// </summary>
        /// <remarks>
        /// Adds a new SubmissionComment with the attributes specified
        /// </remarks>
        /// <param name="submissionId">The ID of the Submission to add the Comment</param>
        /// <param name="submissionComment">The data used to create the SubmissionComment</param>
        /// <param name="ct"></param>
        [HttpPost("submissions/{submissionId}/Comments")]
        [ProducesResponseType(typeof(Submission), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "addSubmissionComment")]
        public async Task<IActionResult> AddSubmissionComment([FromRoute] Guid submissionId, [FromBody] SubmissionComment submissionComment, CancellationToken ct)
        {
            if (!await _submissionService.HasSpecificPermission<Submission>(submissionId, SpecificPermission.Score, ct))
                throw new ForbiddenException();

            var submission = await _submissionService.AddCommentAsync(submissionId, submissionComment, ct);
            return Ok(submission);
        }

        /// <summary>
        /// Updates a  SubmissionComment
        /// </summary>
        /// <remarks>
        /// Updates a SubmissionComment with the attributes specified.
        /// The ID from the route MUST MATCH the ID contained in the submissionComment parameter
        /// </remarks>
        /// <param name="submissionId">The Id of the SubmissionComment to update</param>
        /// <param name="submissionCommentId">The Id of the SubmissionComment to update</param>
        /// <param name="submissionComment">The updated SubmissionComment values</param>
        /// <param name="ct"></param>
        [HttpPut("submissions/{submissionId}/comments/{submissionCommentId}")]
        [ProducesResponseType(typeof(Submission), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "changeSubmissionComment")]
        public async Task<IActionResult> ChangeSubmissionComment([FromRoute] Guid submissionId, [FromRoute] Guid submissionCommentId, [FromBody] SubmissionComment submissionComment, CancellationToken ct)
        {
            if (!await _submissionService.HasSpecificPermission<Submission>(submissionId, SpecificPermission.Score, ct))
                throw new ForbiddenException();

            var submission = await _submissionService.UpdateCommentAsync(submissionId, submissionCommentId, submissionComment, ct);
            return Ok(submission);
        }

        /// <summary>
        /// Deletes a  SubmissionComment
        /// </summary>
        /// <remarks>
        /// Deletes a  SubmissionComment with the specified id
        /// </remarks>
        /// <param name="submissionId">The Id of the SubmissionComment to update</param>
        /// <param name="submissionCommentId">The Id of the SubmissionComment to update</param>
        /// <param name="ct"></param>
        [HttpDelete("submissions/{submissionId}/comments/{submissionCommentId}")]
        [ProducesResponseType(typeof(Submission), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "removeSubmissionComment")]
        public async Task<IActionResult> RemoveSubmissionComment([FromRoute] Guid submissionId, [FromRoute] Guid submissionCommentId, CancellationToken ct)
        {
            if (!await _submissionService.HasSpecificPermission<Submission>(submissionId, SpecificPermission.Score, ct))
                throw new ForbiddenException();

            var submission = await _submissionService.DeleteCommentAsync(submissionId, submissionCommentId, ct);
            return Ok(submission);
        }

    }
}
