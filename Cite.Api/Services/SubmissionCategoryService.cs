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
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface ISubmissionCategoryService
    {
        Task<IEnumerable<SubmissionCategory>> GetForSubmissionAsync(Guid submissionModelId, CancellationToken ct);
        Task<SubmissionCategory> GetAsync(Guid id, CancellationToken ct);
        Task<SubmissionCategory> CreateAsync(SubmissionCategory submissionCategory, CancellationToken ct);
        Task<SubmissionCategory> UpdateAsync(Guid id, SubmissionCategory submissionCategory, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class SubmissionCategoryService : ISubmissionCategoryService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;
        private readonly ISubmissionService _submissionService;

        public SubmissionCategoryService(
            CiteContext context,
            IAuthorizationService authorizationService,
            IPrincipal user,
            IMapper mapper,
            ISubmissionService submissionService)
        {
            _context = context;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
            _submissionService = submissionService;
        }

        public async Task<IEnumerable<SubmissionCategory>> GetForSubmissionAsync(Guid submissionId, CancellationToken ct)
        {
            var submissionCategories = _context.SubmissionCategories.Where(sc => sc.SubmissionId == submissionId);

            return _mapper.Map<IEnumerable<SubmissionCategory>>(await submissionCategories.ToListAsync());
        }

        public async Task<SubmissionCategory> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.SubmissionCategories.SingleOrDefaultAsync(sc => sc.Id == id, ct);

            return _mapper.Map<SubmissionCategory>(item);
        }

        public async Task<SubmissionCategory> CreateAsync(SubmissionCategory submissionCategory, CancellationToken ct)
        {
            submissionCategory.Id = submissionCategory.Id != Guid.Empty ? submissionCategory.Id : Guid.NewGuid();
            submissionCategory.CreatedBy = _user.GetId();
            var submissionCategoryEntity = _mapper.Map<SubmissionCategoryEntity>(submissionCategory);

            _context.SubmissionCategories.Add(submissionCategoryEntity);
            await _context.SaveChangesAsync(ct);
            submissionCategory = await GetAsync(submissionCategoryEntity.Id, ct);

            return submissionCategory;
        }

        public async Task<SubmissionCategory> UpdateAsync(Guid id, SubmissionCategory submissionCategory, CancellationToken ct)
        {
            var submissionCategoryToUpdate = await _context.SubmissionCategories.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (submissionCategoryToUpdate == null)
                throw new EntityNotFoundException<SubmissionCategory>();

            submissionCategory.ModifiedBy = _user.GetId();
            _mapper.Map(submissionCategory, submissionCategoryToUpdate);

            _context.SubmissionCategories.Update(submissionCategoryToUpdate);
            await _context.SaveChangesAsync(ct);

            submissionCategory = await GetAsync(submissionCategoryToUpdate.Id, ct);

            return submissionCategory;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var submissionCategoryToDelete = await _context.SubmissionCategories.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (submissionCategoryToDelete == null)
                throw new EntityNotFoundException<SubmissionCategory>();

            _context.SubmissionCategories.Remove(submissionCategoryToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}
