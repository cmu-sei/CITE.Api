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
using Microsoft.CodeAnalysis.Elfie.Model.Map;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Cite.Api.Hubs;

namespace Cite.Api.Services
{
    public interface IScoringModelService
    {
        Task<IEnumerable<ViewModels.ScoringModel>> GetAsync(ScoringModelGet queryParameters, CancellationToken ct);
        Task<ViewModels.ScoringModel> GetAsync(Guid id, bool hasPermission, bool viewAsAdmin, CancellationToken ct);
        Task<ViewModels.ScoringModel> CreateAsync(ViewModels.ScoringModel scoringModel, CancellationToken ct);
        Task<ViewModels.ScoringModel> CopyAsync(Guid scoringModelId, CancellationToken ct);
        Task<ScoringModelEntity> InternalScoringModelEntityCopyAsync(ScoringModelEntity scoringModelEntity, CancellationToken ct);
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
        private readonly ILogger<IScoringModelService> _logger;
        private readonly IHubContext<MainHub> _mainHub;

        public ScoringModelService(
            CiteContext context,
            IAuthorizationService authorizationService,
            IPrincipal user,
            IMapper mapper,
            ILogger<IScoringModelService> logger,
            IHubContext<MainHub> mainHub)
        {
            _context = context;
            _authorizationService = authorizationService;
            _user = user as ClaimsPrincipal;
            _mapper = mapper;
            _logger = logger;
            _mainHub = mainHub;
        }

        public async Task<IEnumerable<ViewModels.ScoringModel>> GetAsync(ScoringModelGet queryParameters, CancellationToken ct)
        {
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

        public async Task<ViewModels.ScoringModel> GetAsync(Guid id, bool hasPermission, bool viewAsAdmin, CancellationToken ct)
        {
            var item = await _context.ScoringModels
                .Include(sm => sm.ScoringCategories)
                .ThenInclude(sc => sc.ScoringOptions)
                .AsNoTracking()
                .SingleOrDefaultAsync(sm => sm.Id == id, ct);
            if (item.EvaluationId == null && !viewAsAdmin)
                throw new ForbiddenException();
            // make sure the user has permission
            if (!hasPermission && !viewAsAdmin)
            {
                var userId = _user.GetId();
                hasPermission = await _context.TeamMemberships.AnyAsync(m => m.Team.EvaluationId == item.EvaluationId && m.UserId == userId, ct);
                if (!hasPermission)
                    throw new ForbiddenException();
            }
            // only show scoring model calculations to those who can view as Admins
            if (!viewAsAdmin)
            {
                foreach (var scoringCategory in item.ScoringCategories)
                {
                    scoringCategory.CalculationEquation = "Redacted";
                    scoringCategory.ScoringWeight = 0.0;
                }
            }

            return _mapper.Map<ScoringModel>(item);
        }

        public async Task<ViewModels.ScoringModel> CreateAsync(ViewModels.ScoringModel scoringModel, CancellationToken ct)
        {
            scoringModel.Id = scoringModel.Id != Guid.Empty ? scoringModel.Id : Guid.NewGuid();
            scoringModel.CreatedBy = _user.GetId();
            var scoringModelEntity = _mapper.Map<ScoringModelEntity>(scoringModel);

            _context.ScoringModels.Add(scoringModelEntity);
            await _context.SaveChangesAsync(ct);
            scoringModel = await GetAsync(scoringModelEntity.Id, true, true, ct);

            return scoringModel;
        }

        public async Task<ViewModels.ScoringModel> CopyAsync(Guid scoringModelId, CancellationToken ct)
        {
            var scoringModelEntity = await _context.ScoringModels
                .AsNoTracking()
                .Include(m => m.ScoringCategories)
                .ThenInclude(sc => sc.ScoringOptions)
                .AsSplitQuery()
                .SingleOrDefaultAsync(m => m.Id == scoringModelId);
            if (scoringModelEntity == null)
                throw new EntityNotFoundException<ScoringModelEntity>("ScoringModel not found with ID=" + scoringModelId.ToString());

            var newScoringModelEntity = await InternalScoringModelEntityCopyAsync(scoringModelEntity, ct);
            var scoringModel = _mapper.Map<ScoringModel>(newScoringModelEntity);

            return scoringModel;
        }

        public async Task<ScoringModelEntity> InternalScoringModelEntityCopyAsync(ScoringModelEntity scoringModelEntity, CancellationToken ct)
        {
            var currentUserId = _user.GetId();
            var username = (await _context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == _user.GetId(), ct))?.Name ?? "Unknown";
            var newScoringModelId = Guid.NewGuid();
            var originalCategories = scoringModelEntity.ScoringCategories?.ToList() ?? new List<ScoringCategoryEntity>();
            var totalCategories = originalCategories.Count;
            var totalOptions = originalCategories.Sum(c => c.ScoringOptions?.Count ?? 0);

            _logger.LogInformation($"Starting scoring model copy: {scoringModelEntity.Description} ({scoringModelEntity.Id}) -> {newScoringModelId}. Categories: {totalCategories}, Options: {totalOptions}");

            // Disable auto-detect changes for better performance during bulk operations
            _context.ChangeTracker.AutoDetectChangesEnabled = false;

            try
            {
                // Step 1: Create the scoring model using AutoMapper to copy all scalar properties
                var newScoringModel = _mapper.Map<ScoringModelEntity, ScoringModelEntity>(scoringModelEntity);
                newScoringModel.Id = newScoringModelId;
                newScoringModel.CreatedBy = currentUserId;
                newScoringModel.Description = scoringModelEntity.Description + " - " + username;

                _context.ScoringModels.Add(newScoringModel);
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation($"Created scoring model {newScoringModelId}");

                // Step 2: Batch insert scoring categories and options using AutoMapper
                const int batchSize = 10;
                var categoriesProcessed = 0;
                var optionsProcessed = 0;

                for (int i = 0; i < originalCategories.Count; i += batchSize)
                {
                    var batch = originalCategories.Skip(i).Take(batchSize).ToList();

                    foreach (var originalCategory in batch)
                    {
                        var newCategory = _mapper.Map<ScoringCategoryEntity, ScoringCategoryEntity>(originalCategory);
                        newCategory.Id = Guid.NewGuid();
                        newCategory.ScoringModelId = newScoringModelId;
                        newCategory.CreatedBy = currentUserId;
                        newCategory.ScoringOptions = new List<ScoringOptionEntity>();

                        if (originalCategory.ScoringOptions != null)
                        {
                            foreach (var originalOption in originalCategory.ScoringOptions)
                            {
                                var newOption = _mapper.Map<ScoringOptionEntity, ScoringOptionEntity>(originalOption);
                                newOption.Id = Guid.NewGuid();
                                newOption.ScoringCategoryId = newCategory.Id;
                                newOption.CreatedBy = currentUserId;
                                newCategory.ScoringOptions.Add(newOption);
                            }
                        }

                        _context.ScoringCategories.Add(newCategory);
                        categoriesProcessed++;
                        optionsProcessed += newCategory.ScoringOptions.Count;
                    }

                    // Save this batch
                    await _context.SaveChangesAsync(ct);

                    var percentComplete = (int)((categoriesProcessed / (double)totalCategories) * 100);
                    _logger.LogInformation($"Copying scoring model: {percentComplete}% complete ({categoriesProcessed}/{totalCategories} categories, {optionsProcessed}/{totalOptions} options)");

                    // Send progress update via SignalR to admin group
                    await _mainHub.Clients.Group(MainHub.ADMIN_DATA_GROUP).SendAsync(
                        "ScoringModelCopyProgress",
                        new
                        {
                            ScoringModelId = newScoringModelId,
                            PercentComplete = percentComplete,
                            CategoriesProcessed = categoriesProcessed,
                            TotalCategories = totalCategories,
                            OptionsProcessed = optionsProcessed,
                            TotalOptions = totalOptions,
                            Message = $"Copying scoring model: {percentComplete}% complete"
                        },
                        ct);
                }

                _logger.LogInformation($"Successfully copied scoring model {newScoringModelId}. Total: {categoriesProcessed} categories, {optionsProcessed} options");

                // Step 3: Retrieve the complete scoring model with all relationships
                scoringModelEntity = await _context.ScoringModels
                    .Include(m => m.ScoringCategories)
                    .ThenInclude(sc => sc.ScoringOptions)
                    .AsNoTracking()
                    .AsSplitQuery()
                    .SingleOrDefaultAsync(sm => sm.Id == newScoringModelId, ct);

                return scoringModelEntity;
            }
            finally
            {
                // Re-enable auto-detect changes
                _context.ChangeTracker.AutoDetectChangesEnabled = true;
            }
        }

        public async Task<Tuple<MemoryStream, string>> DownloadJsonAsync(Guid scoringModelId, CancellationToken ct)
        {
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
            scoringModelEntity = await InternalScoringModelEntityCopyAsync(scoringModelEntity, ct);

            return _mapper.Map<ScoringModel>(scoringModelEntity);
        }

        public async Task<ViewModels.ScoringModel> UpdateAsync(Guid id, ViewModels.ScoringModel scoringModel, CancellationToken ct)
        {
            var scoringModelToUpdate = await _context.ScoringModels.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (scoringModelToUpdate == null)
                throw new EntityNotFoundException<ScoringModel>();

            scoringModel.ModifiedBy = _user.GetId();
            _mapper.Map(scoringModel, scoringModelToUpdate);

            _context.ScoringModels.Update(scoringModelToUpdate);
            await _context.SaveChangesAsync(ct);

            scoringModel = await GetAsync(scoringModelToUpdate.Id, true, true, ct);

            return scoringModel;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            var scoringModelToDelete = await _context.ScoringModels.SingleOrDefaultAsync(v => v.Id == id, ct);
            if (scoringModelToDelete == null)
                throw new EntityNotFoundException<ScoringModel>();

            _context.ScoringModels.Remove(scoringModelToDelete);
            await _context.SaveChangesAsync(ct);

            return true;
        }

    }
}
