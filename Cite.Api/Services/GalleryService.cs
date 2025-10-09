// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using GAC = Gallery.Api.Client;

namespace Cite.Api.Services
{
    public interface IGalleryService
    {
        Task<GAC.UnreadArticles> GetMyUnreadArticleCountAsync(Guid exhibitId, CancellationToken ct);
    }

    public class GalleryService : IGalleryService
    {
        private readonly ResourceOwnerAuthorizationOptions _resourceOwnerAuthorizationOptions;
        private readonly ClientOptions _clientOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ClaimsPrincipal _user;
        private readonly IAuthorizationService _authorizationService;
        private readonly CiteContext _context;
        private readonly ILogger<GalleryService> _logger;

        public GalleryService(
            IHttpClientFactory httpClientFactory,
            ClientOptions clientOptions,
            IPrincipal user,
            CiteContext citeContext,
            IAuthorizationService authorizationService,
            ILogger<GalleryService> logger,
            ResourceOwnerAuthorizationOptions resourceOwnerAuthorizationOptions)
        {
            _httpClientFactory = httpClientFactory;
            _clientOptions = clientOptions;
            _resourceOwnerAuthorizationOptions = resourceOwnerAuthorizationOptions;
            _user = user as ClaimsPrincipal;
            _authorizationService = authorizationService;
            _context = citeContext;
            _logger = logger;
        }

        public async Task<GAC.UnreadArticles> GetMyUnreadArticleCountAsync(Guid evaluationId, CancellationToken ct)
        {
            // get the evaluation
            var evaluation = await _context.Evaluations.FindAsync(evaluationId);
            if (evaluation == null)
                throw new EntityNotFoundException<EvaluationEntity>("Evaluation not found " + evaluationId.ToString());

            // get the exhibit ID from the evaluation
            var unreadArticles = new GAC.UnreadArticles();
            var galleryExhibitId = evaluation.GalleryExhibitId;
            if (galleryExhibitId != null && _clientOptions.GalleryApiUrl != null && _clientOptions.GalleryApiUrl.Length > 0)
            {
                // send request for the unread article count for the exhibit/user
                var client = ApiClientsExtensions.GetHttpClient(_httpClientFactory, _clientOptions.GalleryApiUrl);
                var tokenResponse = await ApiClientsExtensions.RequestTokenAsync(_resourceOwnerAuthorizationOptions, client);
                client.DefaultRequestHeaders.Add("authorization", $"{tokenResponse.TokenType} {tokenResponse.AccessToken}");
                var galleryApiClient = new GAC.GalleryApiClient(client);
                try
                {
                    unreadArticles = await galleryApiClient.GetUnreadCountAsync((Guid)galleryExhibitId, _user.GetId());
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "The Evaluation (" + evaluation.Id.ToString() + ") has a Gallery Exhibit ID (" + evaluation.GalleryExhibitId.ToString() + "), but there was an error with the Gallery API (" + _clientOptions.GalleryApiUrl + ").");
                }
            }

            return unreadArticles;
        }

    }
}
