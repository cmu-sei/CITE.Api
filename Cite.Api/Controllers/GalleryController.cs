// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Cite.Api.Data.Enumerations;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Services;
using Cite.Api.ViewModels;
using GAC = Gallery.Api.Client;
using Swashbuckle.AspNetCore.Annotations;

namespace Cite.Api.Controllers
{
    public class GalleryController : BaseController
    {
        private readonly IGalleryService _moveService;
        private readonly ICiteAuthorizationService _authorizationService;

        public GalleryController(IGalleryService moveService, ICiteAuthorizationService authorizationService)
        {
            _moveService = moveService;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Gets the evaluation's unread article count for the requesting user
        /// </summary>
        /// <remarks>
        /// Returns the count of unread articles.
        /// </remarks>
        /// <param name="evaluationId"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("evaluations/{evaluationId}/unreadarticlecount")]
        [ProducesResponseType(typeof(GAC.UnreadArticles), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getEvaluationUnreadArticleCount")]
        public async Task<IActionResult> GetEvaluationUnreadArticleCount(Guid evaluationId, CancellationToken ct)
        {
            var unreadArticles = await _moveService.GetMyUnreadArticleCountAsync(evaluationId, ct);
            return Ok(unreadArticles);
        }

    }
}
