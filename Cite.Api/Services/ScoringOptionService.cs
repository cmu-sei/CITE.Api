// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.QueryParameters;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface IScoringOptionService
    {
        Task<IEnumerable<ViewModels.ScoringOption>> GetAsync(ScoringOptionGet queryParameters, CancellationToken ct);
        Task<IEnumerable<ViewModels.ScoringOption>> GetForScoringCategoryAsync(Guid scoringCategoryId, CancellationToken ct);
        Task<ViewModels.ScoringOption> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.ScoringOption> CreateAsync(ViewModels.ScoringOption scoringOption, CancellationToken ct);
        Task<ViewModels.ScoringOption> UpdateAsync(Guid id, ViewModels.ScoringOption scoringOption, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class ScoringOptionService : IScoringOptionService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public ScoringOptionService(
            CiteContext context,
            IAuthorizationService authorizationService,
            IPrincipal user,
            IMapper mapper)
        {
            _context = context;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ViewModels.ScoringOption>> GetAsync(ScoringOptionGet queryParameters, CancellationToken ct)
        {
            IQueryable<ScoringOptionEntity> scoringOptions = null;
            // filter based on description
            if (!String.IsNullOrEmpty(queryParameters.Description))
            {
                scoringOptions = _context.ScoringOptions.Where(sc => sc.Description.Contains(queryParameters.Description));
            }
            else
            {
                scoringOptions = _context.ScoringOptions;
            }

            return _mapper.Map<IEnumerable<ScoringOption>>(await scoringOptions.ToListAsync());
        }

        public async Task<IEnumerable<ViewModels.ScoringOption>> GetForScoringCategoryAsync(Guid scoringCategoryId, CancellationToken ct)
        {
            var scoringOptionList = await _context.ScoringOptions.Where(sc => sc.ScoringCategoryId == scoringCategoryId).ToListAsync();

            return _mapper.Map<IEnumerable<ScoringOption>>(scoringOptionList);
        }

        public async Task<ViewModels.ScoringOption> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.ScoringOptions.SingleOrDefaultAsync(sc => sc.Id == id, ct);

            return _mapper.Map<ScoringOption>(item);
        }

        public async Task<ViewModels.ScoringOption> CreateAsync(ViewModels.ScoringOption scoringOption, CancellationToken ct)
        {
            scoringOption.Id = scoringOption.Id != Guid.Empty ? scoringOption.Id : Guid.NewGuid();
            scoringOption.DateCreated = DateTime.UtcNow;
            scoringOption.CreatedBy = _user.GetId();
            scoringOption.DateModified = null;
            scoringOption.ModifiedBy = null;
            var scoringOptionEntity = _mapper.Map<ScoringOptionEntity>(scoringOption);

            _context.ScoringOptions.Add(scoringOptionEntity);
            await _context.SaveChangesAsync(ct);
            scoringOption = await GetAsync(scoringOptionEntity.Id, ct);

            return scoringOption;
        }

        public async Task<ViewModels.ScoringOption> UpdateAsync(Guid id, ViewModels.ScoringOption scoringOption, CancellationToken ct)
        {
            var scoringOptionToUpdate = await _context.ScoringOptions.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (scoringOptionToUpdate == null)
                throw new EntityNotFoundException<ScoringOption>();

            scoringOption.CreatedBy = scoringOptionToUpdate.CreatedBy;
            scoringOption.DateCreated = scoringOptionToUpdate.DateCreated;
            scoringOption.ModifiedBy = _user.GetId();
            scoringOption.DateModified = DateTime.UtcNow;
            _mapper.Map(scoringOption, scoringOptionToUpdate);
            _context.ScoringOptions.Update(scoringOptionToUpdate);
            await _context.SaveChangesAsync(ct);
            scoringOption = await GetAsync(scoringOptionToUpdate.Id, ct);

            return scoringOption;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var scoringOptionToDelete = await _context.ScoringOptions.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (scoringOptionToDelete == null)
                throw new EntityNotFoundException<ScoringOption>();

            _context.ScoringOptions.Remove(scoringOptionToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}
