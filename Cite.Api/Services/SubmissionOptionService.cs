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
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Infrastructure.QueryParameters;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface ISubmissionOptionService
    {
        Task<IEnumerable<SubmissionOption>> GetForSubmissionCategoryAsync(Guid submissionCategoryId, CancellationToken ct);
        Task<SubmissionOption> GetAsync(Guid id, CancellationToken ct);
        Task<SubmissionOption> CreateAsync(SubmissionOption submissionOption, CancellationToken ct);
        Task<SubmissionOption> UpdateAsync(Guid id, SubmissionOption submissionOption, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class SubmissionOptionService : ISubmissionOptionService
    {
        private readonly CiteContext _context;
        private readonly ISubmissionService _submissionService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;
        private readonly DatabaseOptions _options;

        public SubmissionOptionService(
            CiteContext context,
            ISubmissionService submissionService,
            IAuthorizationService authorizationService,
            IPrincipal user,
            IMapper mapper,
            DatabaseOptions options)
        {
            _context = context;
            _submissionService = submissionService;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
            _options = options;
        }

        public async Task<IEnumerable<SubmissionOption>> GetForSubmissionCategoryAsync(Guid submissionCategoryId, CancellationToken ct)
        {
            var submissionOptions = _context.SubmissionOptions.Where(sc => sc.SubmissionCategoryId == submissionCategoryId).Include(so => so.SubmissionComments);

            return _mapper.Map<IEnumerable<SubmissionOption>>(await submissionOptions.ToListAsync());
        }

        public async Task<SubmissionOption> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.SubmissionOptions
                .Include(so => so.SubmissionComments)
                .SingleOrDefaultAsync(sc => sc.Id == id, ct);

            return _mapper.Map<SubmissionOption>(item);
        }

        public async Task<SubmissionOption> CreateAsync(SubmissionOption submissionOption, CancellationToken ct)
        {
            submissionOption.Id = submissionOption.Id != Guid.Empty ? submissionOption.Id : Guid.NewGuid();
            submissionOption.CreatedBy = _user.GetId();
            var submissionOptionEntity = _mapper.Map<SubmissionOptionEntity>(submissionOption);

            _context.SubmissionOptions.Add(submissionOptionEntity);
            await _context.SaveChangesAsync(ct);
            submissionOption = await GetAsync(submissionOptionEntity.Id, ct);

            return submissionOption;
        }

        public async Task<SubmissionOption> UpdateAsync(Guid id, SubmissionOption submissionOption, CancellationToken ct)
        {
            var submissionOptionToUpdate = await _context.SubmissionOptions.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (submissionOptionToUpdate == null)
                throw new EntityNotFoundException<SubmissionOption>();

            submissionOption.ModifiedBy = _user.GetId();
            _mapper.Map(submissionOption, submissionOptionToUpdate);
            _context.SubmissionOptions.Update(submissionOptionToUpdate);
            await _context.SaveChangesAsync(ct);
            submissionOption = await GetAsync(submissionOptionToUpdate.Id, ct);

            return submissionOption;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var submissionOptionToDelete = await _context.SubmissionOptions.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (submissionOptionToDelete == null)
                throw new EntityNotFoundException<SubmissionOption>();

            _context.SubmissionOptions.Remove(submissionOptionToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}
