// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Cite.Api.Data;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.QueryParameters;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface IScoringModelService
    {
        Task<IEnumerable<ViewModels.ScoringModel>> GetAsync(ScoringModelGet queryParameters, CancellationToken ct);
        Task<ViewModels.ScoringModel> GetAsync(Guid id, CancellationToken ct);
        Task<ViewModels.ScoringModel> CreateAsync(ViewModels.ScoringModel scoringModel, CancellationToken ct);
        Task<ViewModels.ScoringModel> CopyAsync(Guid scoringModelId, CancellationToken ct);
        Task<Tuple<MemoryStream, string>> DownloadJsonAsync(Guid scoringModelId, CancellationToken ct);
        Task<ScoringModel> UploadJsonAsync(FileForm form, CancellationToken ct);
        Task<ViewModels.ScoringModel> UpdateAsync(Guid id, ViewModels.ScoringModel scoringModel, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class ScoringModelService : IScoringModelService
    {
        private readonly CiteContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ClaimsPrincipal _user;
        private readonly IMapper _mapper;

        public ScoringModelService(
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

        public async Task<IEnumerable<ViewModels.ScoringModel>> GetAsync(ScoringModelGet queryParameters, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();


            // filter based on user
            var userId = Guid.Empty;
            var hasUserId = !String.IsNullOrEmpty(queryParameters.UserId);
            if (hasUserId)
            {
                Guid.TryParse(queryParameters.UserId, out userId);
            }
            // filter based on archive status.  NOTE:  Archived scoring models are NOT included by default
            var includeArchived = queryParameters.IncludeArchived != null && (bool)queryParameters.IncludeArchived;
            // filter based on description
            string description = queryParameters.Description;
            var hasDescription = !String.IsNullOrEmpty(description);
            var scoringModelList = await _context.ScoringModels.Where(sm =>
                (!hasUserId || sm.CreatedBy == userId) &&
                (includeArchived || sm.Status != ItemStatus.Archived) &&
                (!hasDescription || sm.Description.Contains(description))
            ).ToListAsync();

            return _mapper.Map<IEnumerable<ScoringModel>>(scoringModelList);
        }

        public async Task<ViewModels.ScoringModel> GetAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new BaseUserRequirement())).Succeeded)
                throw new ForbiddenException();

            var item = await _context.ScoringModels
                .Include(sm => sm.ScoringCategories)
                .ThenInclude(sc => sc.ScoringOptions)
                .SingleOrDefaultAsync(sm => sm.Id == id, ct);

            return _mapper.Map<ScoringModel>(item);
        }

        public async Task<ViewModels.ScoringModel> CreateAsync(ViewModels.ScoringModel scoringModel, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            scoringModel.Id = scoringModel.Id != Guid.Empty ? scoringModel.Id : Guid.NewGuid();
            scoringModel.DateCreated = DateTime.UtcNow;
            scoringModel.CreatedBy = _user.GetId();
            scoringModel.DateModified = null;
            scoringModel.ModifiedBy = null;
            var scoringModelEntity = _mapper.Map<ScoringModelEntity>(scoringModel);

            _context.ScoringModels.Add(scoringModelEntity);
            await _context.SaveChangesAsync(ct);
            scoringModel = await GetAsync(scoringModelEntity.Id, ct);

            return scoringModel;
        }

        public async Task<ViewModels.ScoringModel> CopyAsync(Guid scoringModelId, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();
            
            var scoringModelEntity = await _context.ScoringModels
                .AsNoTracking()
                .Include(m => m.ScoringCategories)
                .ThenInclude(sc => sc.ScoringOptions)
                .AsSplitQuery()
                .SingleOrDefaultAsync(m => m.Id == scoringModelId);
            if (scoringModelEntity == null)
                throw new EntityNotFoundException<ScoringModelEntity>("ScoringModel not found with ID=" + scoringModelId.ToString());

            var newScoringModelEntity = await privateScoringModelCopyAsync(scoringModelEntity, ct);
            var scoringModel = _mapper.Map<ScoringModel>(newScoringModelEntity);

            return scoringModel;
        }

        private async Task<ScoringModelEntity> privateScoringModelCopyAsync(ScoringModelEntity scoringModelEntity, CancellationToken ct)
        {
            var currentUserId = _user.GetId();
            var username = (await _context.Users.SingleOrDefaultAsync(u => u.Id == _user.GetId())).Name;
            scoringModelEntity.Id = Guid.NewGuid();
            scoringModelEntity.DateCreated = DateTime.UtcNow;
            scoringModelEntity.CreatedBy = currentUserId;
            scoringModelEntity.DateModified = scoringModelEntity.DateCreated;
            scoringModelEntity.ModifiedBy = scoringModelEntity.CreatedBy;
            scoringModelEntity.Description = scoringModelEntity.Description + " - " + username;
            var scoringCategoryIdCrossReference = new Dictionary<Guid, Guid>();
            // copy ScoringCategories
            foreach (var scoringCategory in scoringModelEntity.ScoringCategories)
            {
                var newDataFieldId = Guid.NewGuid();
                scoringCategoryIdCrossReference[scoringCategory.Id] = newDataFieldId;
                scoringCategory.Id = newDataFieldId;
                scoringCategory.ScoringModelId = scoringModelEntity.Id;
                scoringCategory.ScoringModel = null;
                scoringCategory.DateCreated = scoringModelEntity.DateCreated;
                scoringCategory.CreatedBy = scoringModelEntity.CreatedBy;
                // copy DataOptions
                foreach (var scoringOption in scoringCategory.ScoringOptions)
                {
                    scoringOption.Id = Guid.NewGuid();
                    scoringOption.ScoringCategoryId = scoringCategory.Id;
                    scoringOption.ScoringCategory = null;
                    scoringOption.DateCreated = scoringModelEntity.DateCreated;
                    scoringOption.CreatedBy = scoringModelEntity.CreatedBy;
                }
            }
            _context.ScoringModels.Add(scoringModelEntity);
            await _context.SaveChangesAsync(ct);

            // get the new ScoringModel to return
            scoringModelEntity = await _context.ScoringModels
                .Include(m => m.ScoringCategories)
                .ThenInclude(sc => sc.ScoringOptions)
                .AsSplitQuery()
                .SingleOrDefaultAsync(sm => sm.Id == scoringModelEntity.Id, ct);

            return scoringModelEntity;
        }

        public async Task<Tuple<MemoryStream, string>> DownloadJsonAsync(Guid scoringModelId, CancellationToken ct)
        {
            // user must be a Content Developer
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var scoringModel = await _context.ScoringModels
                .Include(m => m.ScoringCategories)
                .ThenInclude(sc => sc.ScoringOptions)
                .AsSplitQuery()
                .SingleOrDefaultAsync(sm => sm.Id == scoringModelId, ct);
            if (scoringModel == null)
            {
                throw new EntityNotFoundException<ScoringModelEntity>("ScoringModel not found " + scoringModelId);
            }

            var scoringModelJson = "";
            var options = new JsonSerializerOptions()
            {
                ReferenceHandler = ReferenceHandler.Preserve
            };
            scoringModelJson = JsonSerializer.Serialize(scoringModel, options);
            // convert string to stream
            byte[] byteArray = Encoding.ASCII.GetBytes(scoringModelJson);
            MemoryStream memoryStream = new MemoryStream(byteArray);
            var filename = scoringModel.Description.ToLower().EndsWith(".json") ? scoringModel.Description : scoringModel.Description + ".json";

            return System.Tuple.Create(memoryStream, filename);
        }

        public async Task<ScoringModel> UploadJsonAsync(FileForm form, CancellationToken ct)
        {
            // user must be a Content Developer
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var uploadItem = form.ToUpload;
            var scoringModelJson = "";
            using (StreamReader reader = new StreamReader(uploadItem.OpenReadStream()))
            {
                // convert stream to string
                scoringModelJson = reader.ReadToEnd();
            }
            var options = new JsonSerializerOptions()
            {
                ReferenceHandler = ReferenceHandler.Preserve
            };
            var scoringModelEntity = JsonSerializer.Deserialize<ScoringModelEntity>(scoringModelJson, options);
            // make a copy and add it to the database
            scoringModelEntity = await privateScoringModelCopyAsync(scoringModelEntity, ct);

            return _mapper.Map<ScoringModel>(scoringModelEntity);
        }

        public async Task<ViewModels.ScoringModel> UpdateAsync(Guid id, ViewModels.ScoringModel scoringModel, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var scoringModelToUpdate = await _context.ScoringModels.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (scoringModelToUpdate == null)
                throw new EntityNotFoundException<ScoringModel>();

            scoringModel.CreatedBy = scoringModelToUpdate.CreatedBy;
            scoringModel.DateCreated = scoringModelToUpdate.DateCreated;
            scoringModel.ModifiedBy = _user.GetId();
            scoringModel.DateModified = DateTime.UtcNow;
            _mapper.Map(scoringModel, scoringModelToUpdate);

            _context.ScoringModels.Update(scoringModelToUpdate);
            await _context.SaveChangesAsync(ct);

            scoringModel = await GetAsync(scoringModelToUpdate.Id, ct);

            return scoringModel;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                throw new ForbiddenException();

            var scoringModelToDelete = await _context.ScoringModels.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (scoringModelToDelete == null)
                throw new EntityNotFoundException<ScoringModel>();

            _context.ScoringModels.Remove(scoringModelToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}

