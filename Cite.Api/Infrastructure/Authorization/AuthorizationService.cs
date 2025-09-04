// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cite.Api.Data;
using Cite.Api.ViewModels;
using Cite.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SAVM = Cite.Api.ViewModels;
using Cite.Api.Infrastructure.Exceptions;

namespace Cite.Api.Infrastructure.Authorization;

public interface ICiteAuthorizationService
{
    Task<bool> AuthorizeAsync(
        SystemPermission[] requiredSystemPermissions,
        CancellationToken cancellationToken);

    Task<bool> AuthorizeAsync<T>(
        Guid? resourceId,
        SystemPermission[] requiredSystemPermissions,
        EvaluationPermission[] requiredEvaluationPermissions,
        CancellationToken cancellationToken) where T : IAuthorizationType;

    Task<bool> AuthorizeAsync<T>(
        Guid? resourceId,
        SystemPermission[] requiredSystemPermissions,
        ScoringModelPermission[] requiredScoringModelPermissions,
        CancellationToken cancellationToken) where T : IAuthorizationType;

    IEnumerable<Guid> GetAuthorizedEvaluationIds();
    IEnumerable<SystemPermission> GetSystemPermissions();
    IEnumerable<EvaluationPermissionClaim> GetEvaluationPermissions(Guid? evaluationId = null);
    IEnumerable<ScoringModelPermissionClaim> GetScoringModelPermissions(Guid? scoringModelId = null);
}

public class AuthorizationService(
    IAuthorizationService authService,
    IIdentityResolver identityResolver,
    CiteContext dbContext) : ICiteAuthorizationService
{
    public async Task<bool> AuthorizeAsync(
        SystemPermission[] requiredSystemPermissions,
        CancellationToken cancellationToken)
    {
        return await HasSystemPermission<IAuthorizationType>(requiredSystemPermissions);
    }

    public async Task<bool> AuthorizeAsync<T>(
        Guid? resourceId,
        SystemPermission[] requiredSystemPermissions,
        EvaluationPermission[] requiredEvaluationPermissions,
        CancellationToken cancellationToken) where T : IAuthorizationType
    {
        var claimsPrincipal = identityResolver.GetClaimsPrincipal();
        bool succeeded = await HasSystemPermission<IAuthorizationType>(requiredSystemPermissions);

        if (!succeeded && resourceId.HasValue)
        {
            var evaluationId = await GetEvaluationId<T>(resourceId.Value, cancellationToken);

            if (evaluationId != null)
            {
                var evaluationPermissionRequirement = new EvaluationPermissionRequirement(requiredEvaluationPermissions, evaluationId.Value);
                var evaluationPermissionResult = await authService.AuthorizeAsync(claimsPrincipal, null, evaluationPermissionRequirement);

                succeeded = evaluationPermissionResult.Succeeded;
            }

        }

        return succeeded;
    }

    public async Task<bool> AuthorizeAsync<T>(
        Guid? resourceId,
        SystemPermission[] requiredSystemPermissions,
        ScoringModelPermission[] requiredScoringModelPermissions,
        CancellationToken cancellationToken) where T : IAuthorizationType
    {
        var claimsPrincipal = identityResolver.GetClaimsPrincipal();
        bool succeeded = await HasSystemPermission<IAuthorizationType>(requiredSystemPermissions);

        if (!succeeded && resourceId.HasValue)
        {
            var scoringModelId = await GetScoringModelId<T>(resourceId.Value, cancellationToken);

            if (scoringModelId != null)
            {
                var scoringModelPermissionRequirement = new ScoringModelPermissionRequirement(requiredScoringModelPermissions, scoringModelId.Value);
                var scoringModelPermissionResult = await authService.AuthorizeAsync(claimsPrincipal, null, scoringModelPermissionRequirement);

                succeeded = scoringModelPermissionResult.Succeeded;
            }

        }

        return succeeded;
    }

    public IEnumerable<Guid> GetAuthorizedEvaluationIds()
    {
        return identityResolver.GetClaimsPrincipal().Claims
            .Where(x => x.Type == AuthorizationConstants.EvaluationPermissionClaimType)
            .Select(x => EvaluationPermissionClaim.FromString(x.Value).EvaluationId)
            .ToList();
    }

    public IEnumerable<SystemPermission> GetSystemPermissions()
    {
        var principal = identityResolver.GetClaimsPrincipal();
        var claims = principal.Claims;
        var permissions = claims
           .Where(x => x.Type == AuthorizationConstants.PermissionClaimType)
           .Select(x =>
           {
               if (Enum.TryParse<SystemPermission>(x.Value, out var permission))
                   return permission;

               return (SystemPermission?)null;
           })
           .Where(x => x.HasValue)
           .Select(x => x.Value)
           .ToList();
        return permissions;
    }

    public IEnumerable<EvaluationPermissionClaim> GetEvaluationPermissions(Guid? evaluationId = null)
    {
        var permissions = identityResolver.GetClaimsPrincipal().Claims
           .Where(x => x.Type == AuthorizationConstants.EvaluationPermissionClaimType)
           .Select(x => EvaluationPermissionClaim.FromString(x.Value));

        if (evaluationId.HasValue)
        {
            permissions = permissions.Where(x => x.EvaluationId == evaluationId.Value);
        }

        return permissions;
    }

    public IEnumerable<ScoringModelPermissionClaim> GetScoringModelPermissions(Guid? scoringModelId = null)
    {
        var permissions = identityResolver.GetClaimsPrincipal().Claims
           .Where(x => x.Type == AuthorizationConstants.ScoringModelPermissionClaimType)
           .Select(x => ScoringModelPermissionClaim.FromString(x.Value));

        if (scoringModelId.HasValue)
        {
            permissions = permissions.Where(x => x.ScoringModelId == scoringModelId.Value);
        }

        return permissions;
    }

    private async Task<bool> HasSystemPermission<T>(
        SystemPermission[] requiredSystemPermissions) where T : IAuthorizationType
    {
        var claimsPrincipal = identityResolver.GetClaimsPrincipal();
        var permissionRequirement = new SystemPermissionRequirement(requiredSystemPermissions);
        var permissionResult = await authService.AuthorizeAsync(claimsPrincipal, null, permissionRequirement);

        return permissionResult.Succeeded;
    }

    private async Task<Guid?> GetEvaluationId<T>(Guid resourceId, CancellationToken cancellationToken)
    {
        return typeof(T) switch
        {
            var t when t == typeof(Evaluation) => resourceId,
            var t when t == typeof(SAVM.Task) => await GetEvaluationIdFromTask(resourceId, cancellationToken),
            var t when t == typeof(EvaluationMembership) => await GetEvaluationIdFromEvaluationMembership(resourceId, cancellationToken),
            var t when t == typeof(Result) => await GetEvaluationIdFromResult(resourceId, cancellationToken),
            var t when t == typeof(PlayerView) => await GetEvaluationIdFromPlayerView(resourceId, cancellationToken),
            _ => throw new NotImplementedException($"Handler for type {typeof(T).Name} is not implemented.")
        };
    }

    private async Task<Guid?> GetScoringModelId<T>(Guid resourceId, CancellationToken cancellationToken)
    {
        return typeof(T) switch
        {
            var t when t == typeof(ScoringModel) => resourceId,
            var t when t == typeof(Evaluation) => await GetScoringModelIdFromEvaluation(resourceId, cancellationToken),
            var t when t == typeof(SAVM.Task) => await GetEvaluationIdFromTaskTemplate(resourceId, cancellationToken),
            var t when t == typeof(EvaluationMembership) => await GetScoringModelIdFromScoringModelMembership(resourceId, cancellationToken),
            _ => throw new NotImplementedException($"Handler for type {typeof(T).Name} is not implemented.")
        };
    }

    private async Task<Guid> GetEvaluationIdFromTask(Guid id, CancellationToken cancellationToken)
    {
        return (Guid)await dbContext.Tasks
            .Where(x => x.Id == id)
            .Select(x => x.EvaluationId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid> GetEvaluationIdFromTaskTemplate(Guid id, CancellationToken cancellationToken)
    {
        return (Guid)await dbContext.Tasks
            .Where(x => x.Id == id)
            .Select(x => x.ScoringModelId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid> GetEvaluationIdFromEvaluationMembership(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.EvaluationMemberships
            .Where(x => x.Id == id)
            .Select(x => x.EvaluationId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid> GetEvaluationIdFromResult(Guid id, CancellationToken cancellationToken)
    {
        var taskId = await dbContext.Results
            .Where(x => x.Id == id)
            .Select(x => x.TaskId)
            .FirstOrDefaultAsync(cancellationToken);
        if (taskId == null)
            throw new EntityNotFoundException<Result>();

        return (Guid)await dbContext.Tasks
            .Where(m => m.Id == taskId)
            .Select(m => m.EvaluationId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid> GetEvaluationIdFromPlayerView(Guid id, CancellationToken cancellationToken)
    {
        return (Guid)await dbContext.Evaluations
            .Where(x => x.ViewId == id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid> GetScoringModelIdFromEvaluation(Guid id, CancellationToken cancellationToken)
    {
        return (Guid)await dbContext.Tasks
            .Where(x => x.Id == id)
            .Select(x => x.EvaluationId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid> GetScoringModelIdFromScoringModelMembership(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.ScoringModelMemberships
            .Where(x => x.Id == id)
            .Select(x => x.ScoringModelId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid> GetScoringModelIdFromTask(Guid id, CancellationToken cancellationToken)
    {
        return (Guid)await dbContext.Tasks
            .Where(x => x.Id == id)
            .Select(x => x.ScoringModelId)
            .FirstOrDefaultAsync(cancellationToken);
    }

}
