// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Options;

namespace Cite.Api.Services
{
    public interface IUserClaimsService
    {
        Task<ClaimsPrincipal> AddUserClaims(ClaimsPrincipal principal, bool update);
        Task<ClaimsPrincipal> GetClaimsPrincipal(Guid userId, bool setAsCurrent);
        Task<ClaimsPrincipal> RefreshClaims(Guid userId);
        ClaimsPrincipal GetCurrentClaimsPrincipal();
        void SetCurrentClaimsPrincipal(ClaimsPrincipal principal);
    }

    public class UserClaimsService : IUserClaimsService
    {
        private readonly CiteContext _context;
        private readonly ClaimsTransformationOptions _options;
        private IMemoryCache _cache;
        private ClaimsPrincipal _currentClaimsPrincipal;

        public UserClaimsService(CiteContext context, IMemoryCache cache, ClaimsTransformationOptions options)
        {
            _context = context;
            _options = options;
            _cache = cache;
        }

        public async Task<ClaimsPrincipal> AddUserClaims(ClaimsPrincipal principal, bool update)
        {
            List<Claim> claims;
            var identity = ((ClaimsIdentity)principal.Identity);
            var userId = principal.GetId();

            if (!_cache.TryGetValue(userId, out claims))
            {
                claims = new List<Claim>();
                var user = await ValidateUser(userId, principal.FindFirst("name")?.Value, update);

                if(user != null)
                {
                    claims.AddRange(await GetUserClaims(userId));

                    if (_options.EnableCaching)
                    {
                        _cache.Set(userId, claims, new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(_options.CacheExpirationSeconds)));
                    }
                }
            }
            addNewClaims(identity, claims);
            return principal;
        }

        public async Task<ClaimsPrincipal> GetClaimsPrincipal(Guid userId, bool setAsCurrent)
        {
            ClaimsIdentity identity = new ClaimsIdentity();
            identity.AddClaim(new Claim("sub", userId.ToString()));
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);

            principal = await AddUserClaims(principal, false);

            if (setAsCurrent || _currentClaimsPrincipal.GetId() == userId)
            {
                _currentClaimsPrincipal = principal;
            }

            return principal;
        }

        public async Task<ClaimsPrincipal> RefreshClaims(Guid userId)
        {
            _cache.Remove(userId);
            return await GetClaimsPrincipal(userId, false);
        }

        public ClaimsPrincipal GetCurrentClaimsPrincipal()
        {
            return _currentClaimsPrincipal;
        }

        public void SetCurrentClaimsPrincipal(ClaimsPrincipal principal)
        {
            _currentClaimsPrincipal = principal;
        }

        private async Task<UserEntity> ValidateUser(Guid subClaim, string nameClaim, bool update)
        {
            var user = await _context.Users
                .Where(u => u.Id == subClaim)
                .SingleOrDefaultAsync();

            var anyUsers = await _context.Users.AnyAsync();

            if(update)
            {
                if (user == null)
                {
                    user = new UserEntity
                    {
                        Id = subClaim,
                        Name = nameClaim ?? "Anonymous"
                    };

                    // First user is default SystemAdmin
                    if (!anyUsers)
                    {
                        var systemAdminPermission = await _context.Permissions.Where(p => p.Key == CiteClaimTypes.SystemAdmin.ToString()).FirstOrDefaultAsync();

                        if (systemAdminPermission != null)
                        {
                            user.UserPermissions.Add(new UserPermissionEntity(user.Id, systemAdminPermission.Id));
                        }
                    }

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    if (nameClaim != null && user.Name != nameClaim)
                    {
                        user.Name = nameClaim;
                        _context.Update(user);
                        await _context.SaveChangesAsync();
                    }
                }
            }

            return user;
        }

        private async Task<IEnumerable<Claim>> GetUserClaims(Guid userId)
        {
            List<Claim> claims = new List<Claim>();
            // User level permissions
            var userPermissions = await _context.UserPermissions
                .Where(u => u.UserId == userId)
                .Include(x => x.Permission)
                .ToArrayAsync();

            foreach (var userPermission in userPermissions)
            {
                CiteClaimTypes citeClaim;
                if (Enum.TryParse<CiteClaimTypes>(userPermission.Permission.Key, out citeClaim))
                {
                    claims.Add(new Claim(citeClaim.ToString(), "true"));
                }
            }
            // Object Permissions
            var teamUserList = await _context.TeamUsers
                .Include(tu => tu.Team)
                .Where(x => x.UserId == userId)
                .ToListAsync();
            var teamIdList = new List<string>();
            var evaluationIdList = new List<string>();
            var observerEvaluationIdList = new List<string>();
            var incrementerEvaluationIdList = new List<string>();
            var modifierEvaluationIdList = new List<string>();
            var submitterEvaluationIdList = new List<string>();
            // add IDs of allowed teams
            foreach (var teamUser in teamUserList)
            {
                teamIdList.Add(teamUser.TeamId.ToString());
                var teamEvaluationIdList = await _context.Teams
                    .Where(x => x.Id == teamUser.TeamId)
                    .Select(x => x.EvaluationId)
                    .ToListAsync();
                foreach (var id in teamEvaluationIdList)
                {
                    if (!evaluationIdList.Contains(id.ToString()))
                    {
                        evaluationIdList.Add(id.ToString());
                    }
                }
                if (teamUser.IsObserver)
                {
                    observerEvaluationIdList.Add(teamUser.Team.EvaluationId.ToString());
                }
                if (teamUser.CanIncrementMove)
                {
                    incrementerEvaluationIdList.Add(teamUser.Team.EvaluationId.ToString());
                }
                if (teamUser.CanModify)
                {
                    modifierEvaluationIdList.Add(teamUser.Team.EvaluationId.ToString());
                }
                if (teamUser.CanSubmit)
                {
                    submitterEvaluationIdList.Add(teamUser.Team.EvaluationId.ToString());
                }
            }
            // add IDs of allowed teams
            claims.Add(new Claim(CiteClaimTypes.TeamUser.ToString(), String.Join(",", teamIdList.ToArray())));
            // add IDs of allowed evaluations
            claims.Add(new Claim(CiteClaimTypes.EvaluationUser.ToString(), String.Join(",", evaluationIdList.ToArray())));
            // add IDs of observer evaluations
            claims.Add(new Claim(CiteClaimTypes.EvaluationObserver.ToString(), String.Join(",", observerEvaluationIdList.ToArray())));
            // add IDs of incrementer evaluations
            claims.Add(new Claim(CiteClaimTypes.CanIncrementMove.ToString(), String.Join(",", incrementerEvaluationIdList.ToArray())));
            // add IDs of modifier evaluations
            claims.Add(new Claim(CiteClaimTypes.CanModify.ToString(), String.Join(",", modifierEvaluationIdList.ToArray())));
            // add IDs of submitter evaluations
            claims.Add(new Claim(CiteClaimTypes.CanSubmit.ToString(), String.Join(",", submitterEvaluationIdList.ToArray())));

            return claims;
        }

        private void addNewClaims(ClaimsIdentity identity, List<Claim> claims)
        {
            var newClaims = new List<Claim>();
            claims.ForEach(delegate(Claim claim)
            {
                if (!identity.Claims.Any(identityClaim => identityClaim.Type == claim.Type))
                {
                    newClaims.Add(claim);
                }
            });
            identity.AddClaims(newClaims);
        }
    }
}

