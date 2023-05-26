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
    public class SubmissionController : BaseController
    {
        private readonly ISubmissionService _submissionService;
        private readonly IAuthorizationService _authorizationService;

        public SubmissionController(ISubmissionService submissionService, IAuthorizationService authorizationService)
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
            var list = await _submissionService.GetAsync(queryParameters, ct);
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
        [HttpGet("evaluations/{evaluationId}/submissions/mine")]
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
            submission.CreatedBy = User.GetId();
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
            submission.ModifiedBy = User.GetId();
            var updatedSubmission = await _submissionService.UpdateAsync(id, submission, ct);
            return Ok(updatedSubmission);
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
            var updatedSubmission = await _submissionService.PresetSelectionsAsync(id, ct);
            return Ok(updatedSubmission);
        }

        /// <summary>
        /// Fills in the details for a team average submission
        /// </summary>
        /// <remarks>
        /// Fills in the categories, options and comments for the team average submission
        /// </remarks>
        /// <param name="submission">The team average Submission needing details</param>
        /// <param name="ct"></param>
        [HttpPut("submissions/teamavg")]
        [ProducesResponseType(typeof(Submission), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "fillTeamAverageSubmission")]
        public async Task<IActionResult> FillTeamAverageSubmission([FromBody] Submission submission, CancellationToken ct)
        {
            var updatedSubmission = await _submissionService.FillTeamAverageAsync(submission, ct);
            return Ok(updatedSubmission);
        }

        /// <summary>
        /// Fills in the details for a teamType average submission
        /// </summary>
        /// <remarks>
        /// Fills in the categories, options and comments for the teamType average submission
        /// </remarks>
        /// <param name="submission">The teamType average Submission needing details</param>
        /// <param name="ct"></param>
        [HttpPut("submissions/teamtypeavg")]
        [ProducesResponseType(typeof(Submission), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "fillTeamTypeAverageSubmission")]
        public async Task<IActionResult> FillTeamTypeAverageSubmission([FromBody] Submission submission, CancellationToken ct)
        {
            var updatedSubmission = await _submissionService.FillTeamTypeAverageAsync(submission, ct);
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
            await _submissionService.DeleteAsync(id, ct);
            return NoContent();
        }

    }
}

