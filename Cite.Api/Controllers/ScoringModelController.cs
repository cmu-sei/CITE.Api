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
    public class ScoringModelController : BaseController
    {
        private readonly IScoringModelService _scoringModelService;
        private readonly IAuthorizationService _authorizationService;

        public ScoringModelController(IScoringModelService scoringModelService, IAuthorizationService authorizationService)
        {
            _scoringModelService = scoringModelService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets ScoringModels
        /// </summary>
        /// <remarks>
        /// Returns a list of ScoringModels.
        /// </remarks>
        /// <param name="queryParameters">Result filtering criteria</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("scoringModels")]
        [ProducesResponseType(typeof(IEnumerable<ScoringModel>), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getScoringModels")]
        public async Task<IActionResult> Get([FromQuery] ScoringModelGet queryParameters, CancellationToken ct)
        {
            var list = await _scoringModelService.GetAsync(queryParameters, ct);
            return Ok(list);
        }

        /// <summary>
        /// Gets a specific ScoringModel by id
        /// </summary>
        /// <remarks>
        /// Returns the ScoringModel with the id specified
        /// </remarks>
        /// <param name="id">The id of the ScoringModel</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("scoringModels/{id}")]
        [ProducesResponseType(typeof(ScoringModel), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getScoringModel")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var scoringModel = await _scoringModelService.GetAsync(id, ct);

            if (scoringModel == null)
                throw new EntityNotFoundException<ScoringModel>();

            return Ok(scoringModel);
        }

        /// <summary>
        /// Creates a new ScoringModel
        /// </summary>
        /// <remarks>
        /// Creates a new ScoringModel with the attributes specified
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="scoringModel">The data used to create the ScoringModel</param>
        /// <param name="ct"></param>
        [HttpPost("scoringModels")]
        [ProducesResponseType(typeof(ScoringModel), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "createScoringModel")]
        public async Task<IActionResult> Create([FromBody] ScoringModel scoringModel, CancellationToken ct)
        {
            scoringModel.CreatedBy = User.GetId();
            var createdScoringModel = await _scoringModelService.CreateAsync(scoringModel, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdScoringModel.Id }, createdScoringModel);
        }

        /// <summary>
        /// Creates a new ScoringModel by copying an existing ScoringModel
        /// </summary>
        /// <remarks>
        /// Creates a new ScoringModel from the specified existing ScoringModel
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The ID of the ScoringModel to be copied</param>
        /// <param name="ct"></param>
        [HttpPost("scoringModels/{id}/copy")]
        [ProducesResponseType(typeof(ScoringModel), (int)HttpStatusCode.Created)]
        [SwaggerOperation(OperationId = "copyScoringModel")]
        public async Task<IActionResult> Copy(Guid id, CancellationToken ct)
        {
            var createdScoringModel = await _scoringModelService.CopyAsync(id, ct);
            return CreatedAtAction(nameof(this.Get), new { id = createdScoringModel.Id }, createdScoringModel);
        }

        /// <summary>
        /// Updates a  ScoringModel
        /// </summary>
        /// <remarks>
        /// Updates a ScoringModel with the attributes specified.
        /// The ID from the route MUST MATCH the ID contained in the scoringModel parameter
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The Id of the ScoringModel to update</param>
        /// <param name="scoringModel">The updated ScoringModel values</param>
        /// <param name="ct"></param>
        [HttpPut("scoringModels/{id}")]
        [ProducesResponseType(typeof(ScoringModel), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "updateScoringModel")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] ScoringModel scoringModel, CancellationToken ct)
        {
            scoringModel.ModifiedBy = User.GetId();
            var updatedScoringModel = await _scoringModelService.UpdateAsync(id, scoringModel, ct);
            return Ok(updatedScoringModel);
        }

        /// <summary>
        /// Deletes a  ScoringModel
        /// </summary>
        /// <remarks>
        /// Deletes a  ScoringModel with the specified id
        /// <para />
        /// Accessible only to a ContentDeveloper or an Administrator
        /// </remarks>
        /// <param name="id">The id of the ScoringModel to delete</param>
        /// <param name="ct"></param>
        [HttpDelete("scoringModels/{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [SwaggerOperation(OperationId = "deleteScoringModel")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _scoringModelService.DeleteAsync(id, ct);
            return NoContent();
        }

        /// <summary> Upload a json ScoringModel file </summary>
        /// <param name="form"> The files to upload and their settings </param>
        /// <param name="ct"></param>
        [HttpPost("scoringModels/json")]
        [ProducesResponseType(typeof(ScoringModel), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "uploadJsonFiles")]
        public async Task<IActionResult> UploadJsonAsync([FromForm] FileForm form, CancellationToken ct)
        {
            var result = await _scoringModelService.UploadJsonAsync(form, ct);
            return Ok(result);
        }

        /// <summary> Download a ScoringModel by id as json file </summary>
        /// <param name="id"> The id of the scoringModel </param>
        /// <param name="ct"></param>
        [HttpGet("scoringModels/{id}/json")]
        [ProducesResponseType(typeof(FileResult), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "downloadJson")]
        public async Task<IActionResult> DownloadJsonAsync(Guid id, CancellationToken ct)
        {
            (var stream, var fileName) = await _scoringModelService.DownloadJsonAsync(id, ct);

            // If this is wrapped in an Ok, it throws an exception
            return File(stream, "application/octet-stream", fileName);
        }

    }
}

