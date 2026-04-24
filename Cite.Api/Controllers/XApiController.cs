// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Cite.Api.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace Cite.Api.Controllers
{
    public class XApiController : BaseController
    {
        private readonly IXApiService _xApiService;

        public XApiController(IXApiService xApiService)
        {
            _xApiService = xApiService;
        }

        /// <summary>
        /// Logs xAPI observed statement for Dashboard by Evaluation id and Team id
        /// </summary>
        /// <remarks>
        /// Returns status
        /// </remarks>
        /// <param name="evaluationId">The id of the Evaluation</param>
        /// <param name="teamId">The id of the Team</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("xapi/observed/evaluation/{evaluationId}/team/{teamId}/dashboard")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "observedEvaluationDashboard")]
        public async Task<IActionResult> ObservedEvaluationDashboard(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            if (!await _xApiService.EvaluationDashboardObservedAsync(evaluationId, teamId, ct))
                throw new Exception();

            return Ok();
        }

        /// <summary>
        /// Logs xAPI observed statement for Scoresheet by Evaluation id and Team id
        /// </summary>
        /// <remarks>
        /// Returns status
        /// </remarks>
        /// <param name="evaluationId">The id of the Evaluation</param>
        /// <param name="teamId">The id of the Team</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("xapi/observed/evaluation/{evaluationId}/team/{teamId}/scoresheet")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "observedEvaluationScoresheet")]
        public async Task<IActionResult> ObservedEvaluationScoresheet(Guid evaluationId, Guid teamId, CancellationToken ct)
        {
            if (!await _xApiService.EvaluationScoresheetObservedAsync(evaluationId, teamId, ct))
                throw new Exception();

            return Ok();
        }
    }
}
