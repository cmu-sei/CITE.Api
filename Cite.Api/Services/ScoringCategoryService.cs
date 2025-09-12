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
    public interface IScoringCategoryService
    {
        Task<IEnumerable<ViewModels.ScoringCategory>> GetAsync(ScoringCategoryGet queryParameters, CancellationToken ct);
        Task<IEnumerable<ViewModels.ScoringCategory>> GetForScoringModelAsync(Guid scoringModelId, bool includeCalculations, CancellationToken ct);
        Task<ViewModels.ScoringCategory> GetAsync(Guid id, bool includeCalculations, CancellationToken ct);
        Task<ViewModels.ScoringCategory> CreateAsync(ViewModels.ScoringCategory scoringCategory, CancellationToken ct);
        Task<ViewModels.ScoringCategory> UpdateAsync(Guid id, ViewModels.ScoringCategory scoringCategory, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class ScoringCategoryService : IScoringCategoryService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public ScoringCategoryService(
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

        public async Task<IEnumerable<ViewModels.ScoringCategory>> GetAsync(ScoringCategoryGet queryParameters, CancellationToken ct)
        {
            IQueryable<ScoringCategoryEntity> scoringCategories = null;
            // filter based on description
            if (!String.IsNullOrEmpty(queryParameters.Description))
            {
                scoringCategories = _context.ScoringCategories.Where(sc => sc.Description.Contains(queryParameters.Description));
            }
            else
            {
                scoringCategories = _context.ScoringCategories;
            }

            return _mapper.Map<IEnumerable<ScoringCategory>>(await scoringCategories.ToListAsync());
        }

        public async Task<IEnumerable<ViewModels.ScoringCategory>> GetForScoringModelAsync(Guid scoringModelId, bool includeCalculations, CancellationToken ct)
        {
            var scoringCategoryList = await _context.ScoringCategories.Where(sc =>
                sc.ScoringModelId == scoringModelId &&
                (includeCalculations || (sc.ScoringModel.EvaluationId != null))
            ).ToListAsync();
            // only show scoring model calculations to content developers and system admins
            if (!includeCalculations)
            {
                foreach (var scoringCategory in scoringCategoryList)
                {
                    scoringCategory.CalculationEquation = "********";
                    scoringCategory.ScoringWeight = 0.0;
                }
            }

            return _mapper.Map<IEnumerable<ScoringCategory>>(scoringCategoryList);
        }

        public async Task<ViewModels.ScoringCategory> GetAsync(Guid id, bool includeCalculations, CancellationToken ct)
        {
            var item = await _context.ScoringCategories.SingleOrDefaultAsync(sc => sc.Id == id, ct);
            // only show scoring model calculations to content developers and system admins
            if (!includeCalculations)
            {
                item.CalculationEquation = "********";
                item.ScoringWeight = 0.0;
            }

            return _mapper.Map<ScoringCategory>(item);
        }

        public async Task<ViewModels.ScoringCategory> CreateAsync(ViewModels.ScoringCategory scoringCategory, CancellationToken ct)
        {
            scoringCategory.Id = scoringCategory.Id != Guid.Empty ? scoringCategory.Id : Guid.NewGuid();
            scoringCategory.DateCreated = DateTime.UtcNow;
            scoringCategory.CreatedBy = _user.GetId();
            scoringCategory.DateModified = null;
            scoringCategory.ModifiedBy = null;
            var scoringCategoryEntity = _mapper.Map<ScoringCategoryEntity>(scoringCategory);
            _context.ScoringCategories.Add(scoringCategoryEntity);
            await _context.SaveChangesAsync(ct);
            scoringCategory = await GetAsync(scoringCategoryEntity.Id, true, ct);

            return scoringCategory;
        }

        public async Task<ViewModels.ScoringCategory> UpdateAsync(Guid id, ViewModels.ScoringCategory scoringCategory, CancellationToken ct)
        {
            var scoringCategoryToUpdate = await _context.ScoringCategories.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (scoringCategoryToUpdate == null)
                throw new EntityNotFoundException<ScoringCategory>();

            scoringCategory.CreatedBy = scoringCategoryToUpdate.CreatedBy;
            scoringCategory.DateCreated = scoringCategoryToUpdate.DateCreated;
            scoringCategory.ModifiedBy = _user.GetId();
            scoringCategory.DateModified = DateTime.UtcNow;
            _mapper.Map(scoringCategory, scoringCategoryToUpdate);
            _context.ScoringCategories.Update(scoringCategoryToUpdate);
            await _context.SaveChangesAsync(ct);
            scoringCategory = await GetAsync(scoringCategoryToUpdate.Id, true, ct);

            return scoringCategory;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var scoringCategoryToDelete = await _context.ScoringCategories.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (scoringCategoryToDelete == null)
                throw new EntityNotFoundException<ScoringCategory>();

            _context.ScoringCategories.Remove(scoringCategoryToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}
