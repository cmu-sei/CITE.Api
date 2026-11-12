// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Crucible.Common.Authentication.Claims;
using Cite.Api.Data;
using Cite.Api.Data.Enumerations;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Options;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Text.RegularExpressions;

namespace Cite.Api.Services
{
    public interface IUserClaimsService
    {
        Task<ClaimsPrincipal> AddUserClaims(ClaimsPrincipal principal, bool update);
        Task<ClaimsPrincipal> GetClaimsPrincipal(Guid userId, bool setAsCurrent);
        Task<ClaimsPrincipal> RefreshClaims(Guid userId);
        Task<ClaimsPrincipal> RefreshClaims(ClaimsPrincipal principal);
        ClaimsPrincipal GetCurrentClaimsPrincipal();
        void SetCurrentClaimsPrincipal(ClaimsPrincipal principal);
    }

    public class UserClaimsService : IUserClaimsService
    {
        private readonly CiteContext _context;
        private readonly ClaimsTransformationOptions _options;
        private readonly IPermissionClaimsCache _cache;
        private ClaimsPrincipal _currentClaimsPrincipal;

        public UserClaimsService(CiteContext context, IPermissionClaimsCache cache, ClaimsTransformationOptions options)
        {
            _context = context;
            _options = options;
            _cache = cache;
        }

        /// <summary>
        /// Adds the user's permission claims from the database to the principal. They are cached per user and per
        /// fingerprint of what the load reads from the token: the realm roles and groups (each only while it is used
        /// from the identity provider) and the name. A new token with the same roles, groups and name, as every silent
        /// renew and every other tab has, reuses the entry; the token id plays no part.
        /// </summary>
        /// <remarks>
        /// The user row is created or renamed inside the cached load, so it costs nothing on a hit. That is enough
        /// because a user with no row is never cached (the next request loads again and creates it), and the name is
        /// in the fingerprint, so a token carrying a new name misses and writes it.
        /// </remarks>
        public async Task<ClaimsPrincipal> AddUserClaims(ClaimsPrincipal principal, bool update)
        {
            var identity = (ClaimsIdentity)principal.Identity;
            var userId = principal.GetId();
            var name = principal.FindFirst("name")?.Value;

            var tokenRoleNames = _options.UseRolesFromIdP ?
                this.GetClaimsFromToken(principal, _options.RolesClaimPath).Select(x => x.ToLower()).ToArray() :
                [];

            var tokenGroupNames = _options.UseGroupsFromIdP ?
                this.GetClaimsFromToken(principal, _options.GroupsClaimPath).Select(x => x.ToLower()).ToArray() :
                [];

            Task<PermissionClaimsLoadResult> load() => LoadClaims(userId, name, update, tokenRoleNames, tokenGroupNames);

            var claims = _options.EnableCaching ?
                await _cache.GetOrLoadAsync(userId, PermissionClaimsFingerprint.Create(ignoreCase: false, tokenRoleNames, tokenGroupNames, [name ?? string.Empty]), load) :
                (await load()).Claims;

            addNewClaims(identity, claims);
            return principal;
        }

        private async Task<PermissionClaimsLoadResult> LoadClaims(Guid userId, string name, bool update, string[] tokenRoleNames, string[] tokenGroupNames)
        {
            var user = await ValidateUser(userId, name, update);

            // no row (and update off): nothing to grant, and nothing to keep, so the next request with update creates it
            if (user == null)
            {
                return new PermissionClaimsLoadResult([], Cacheable: false);
            }

            return new PermissionClaimsLoadResult((await GetPermissionClaims(userId, tokenRoleNames, tokenGroupNames)).ToList());
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
            _cache.Invalidate(userId);
            return await GetClaimsPrincipal(userId, false);
        }

        /// <summary>
        /// The principal with its permission claims built again from the database, for a principal that outlives the
        /// request that added them, such as a SignalR connection's. The token's own claims are kept, so roles and groups
        /// from the identity provider still apply.
        /// </summary>
        public async Task<ClaimsPrincipal> RefreshClaims(ClaimsPrincipal principal)
        {
            _cache.Invalidate(principal.GetId());
            var identity = (ClaimsIdentity)principal.Identity;
            var tokenClaims = identity.Claims.Where(x => !PermissionClaimTypes.Contains(x.Type));
            var refreshed = new ClaimsPrincipal(new ClaimsIdentity(tokenClaims, identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType));

            return await AddUserClaims(refreshed, false);
        }

        private static readonly string[] PermissionClaimTypes =
        [
            AuthorizationConstants.PermissionClaimType,
            AuthorizationConstants.EvaluationPermissionClaimType,
            AuthorizationConstants.ScoringModelPermissionClaimType,
            AuthorizationConstants.TeamPermissionClaimType,
        ];

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

            if (update)
            {
                if (user == null)
                {
                    user = new UserEntity
                    {
                        Id = subClaim,
                        Name = nameClaim ?? "Anonymous",
                        CreatedBy = subClaim
                    };

                    _context.Users.Add(user);
                }
                else
                {
                    if (nameClaim != null && user.Name != nameClaim)
                    {
                        user.Name = nameClaim;
                        _context.Update(user);
                    }
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception) { }
            }

            return user;
        }

        /// <param name="userId">The user whose memberships and system role are read.</param>
        /// <param name="tokenRoleNames">The token's realm roles, lower case; empty unless roles come from the identity provider.</param>
        /// <param name="tokenGroupNames">The token's groups, lower case; empty unless groups come from the identity provider.</param>
        private async Task<IEnumerable<Claim>> GetPermissionClaims(Guid userId, string[] tokenRoleNames, string[] tokenGroupNames)
        {
            List<Claim> claims = new();

            var roles = await _context.SystemRoles
                .Where(x => tokenRoleNames.Contains(x.Name.ToLower()))
                .ToListAsync();

            var userRole = await _context.Users
                .Where(x => x.Id == userId)
                .Select(x => x.Role)
                .FirstOrDefaultAsync();

            if (userRole != null)
            {
                roles.Add(userRole);
            }

            roles = roles.Distinct().ToList();

            foreach (var role in roles)
            {
                List<string> permissions;

                if (role.AllPermissions)
                {
                    permissions = Enum.GetValues<SystemPermission>().Select(x => x.ToString()).ToList();
                }
                else
                {
                    permissions = role.Permissions.Select(x => x.ToString()).ToList();
                }

                foreach (var permission in permissions)
                {
                    if (!claims.Any(x => x.Type == AuthorizationConstants.PermissionClaimType &&
                        x.Value == permission))
                    {
                        claims.Add(new Claim(AuthorizationConstants.PermissionClaimType, permission));
                    }
                    ;
                }
            }

            var groupIds = await _context.Groups
                .Where(x => x.Memberships.Any(y => y.UserId == userId) || tokenGroupNames.Contains(x.Name.ToLower()))
                .Select(x => x.Id)
                .ToListAsync();

            // Get Evaluation Permissions
            var evaluationMemberships = await _context.EvaluationMemberships
                .Where(x => x.UserId == userId || (x.GroupId.HasValue && groupIds.Contains(x.GroupId.Value)))
                .Include(x => x.Role)
                .GroupBy(x => x.EvaluationId)
                .ToListAsync();

            foreach (var group in evaluationMemberships)
            {
                var evaluationPermissions = new List<EvaluationPermission>();

                foreach (var membership in group)
                {
                    if (membership.Role.AllPermissions)
                    {
                        evaluationPermissions.AddRange(Enum.GetValues<EvaluationPermission>());
                    }
                    else
                    {
                        evaluationPermissions.AddRange(membership.Role.Permissions);
                    }
                }

                var permissionsClaim = new EvaluationPermissionClaim
                {
                    EvaluationId = group.Key,
                    Permissions = evaluationPermissions.Distinct().ToArray()
                };

                claims.Add(new Claim(AuthorizationConstants.EvaluationPermissionClaimType, permissionsClaim.ToString()));
            }

            // Get ScoringModel Permissions
            var scoringModelMemberships = await _context.ScoringModelMemberships
                .Where(x => x.UserId == userId || (x.GroupId.HasValue && groupIds.Contains(x.GroupId.Value)))
                .Include(x => x.Role)
                .GroupBy(x => x.ScoringModelId)
                .ToListAsync();

            foreach (var group in scoringModelMemberships)
            {
                var scoringModelPermissions = new List<ScoringModelPermission>();

                foreach (var membership in group)
                {
                    if (membership.Role.AllPermissions)
                    {
                        scoringModelPermissions.AddRange(Enum.GetValues<ScoringModelPermission>());
                    }
                    else
                    {
                        scoringModelPermissions.AddRange(membership.Role.Permissions);
                    }
                }

                var permissionsClaim = new ScoringModelPermissionClaim
                {
                    ScoringModelId = group.Key,
                    Permissions = scoringModelPermissions.Distinct().ToArray()
                };

                claims.Add(new Claim(AuthorizationConstants.ScoringModelPermissionClaimType, permissionsClaim.ToString()));
            }

            // Get Team Permissions
            var teamMemberships = await _context.TeamMemberships
                .Where(x => x.UserId == userId)
                .Include(x => x.Role)
                .GroupBy(x => x.TeamId)
                .ToListAsync();

            foreach (var group in teamMemberships)
            {
                var teamPermissions = new List<TeamPermission>();

                foreach (var membership in group)
                {
                    if (membership.Role != null && membership.Role.Permissions.Count > 0)
                    {
                        teamPermissions.AddRange(membership.Role.Permissions);
                    }
                }

                var permissionsClaim = new TeamPermissionClaim
                {
                    TeamId = group.Key,
                    Permissions = teamPermissions.Distinct().ToArray()
                };

                claims.Add(new Claim(AuthorizationConstants.TeamPermissionClaimType, permissionsClaim.ToString()));
            }

            return claims;
        }

        private string[] GetClaimsFromToken(ClaimsPrincipal principal, string claimPath)
        {
            if (string.IsNullOrEmpty(claimPath))
            {
                return [];
            }

            // Name of the claim to insert into the token. This can be a fully qualified name like 'address.street'.
            // In this case, a nested json object will be created. To prevent nesting and use dot literally, escape the dot with backslash (\.).
            var pathSegments = Regex.Split(claimPath, @"(?<!\\)\.").Select(s => s.Replace("\\.", ".")).ToArray();

            var tokenClaim = principal.Claims.Where(x => x.Type == pathSegments.First()).FirstOrDefault();

            if (tokenClaim == null)
            {
                return [];
            }

            return tokenClaim.ValueType switch
            {
                ClaimValueTypes.String => [tokenClaim.Value],
                JsonClaimValueTypes.Json => ExtractJsonClaimValues(tokenClaim.Value, pathSegments.Skip(1)),
                _ => []
            };
        }

        private string[] ExtractJsonClaimValues(string json, IEnumerable<string> pathSegments)
        {
            List<string> values = new();
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement currentElement = doc.RootElement;

                foreach (var segment in pathSegments)
                {
                    if (!currentElement.TryGetProperty(segment, out JsonElement propertyElement))
                    {
                        return [];
                    }

                    currentElement = propertyElement;
                }

                if (currentElement.ValueKind == JsonValueKind.Array)
                {
                    values.AddRange(currentElement.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString()));
                }
                else if (currentElement.ValueKind == JsonValueKind.String)
                {
                    values.Add(currentElement.GetString());
                }
            }
            catch (JsonException)
            {
                // Handle invalid JSON format
            }

            return values.ToArray();
        }

        private void addNewClaims(ClaimsIdentity identity, IReadOnlyList<Claim> claims)
        {
            var newClaims = new List<Claim>();
            foreach (var claim in claims)
            {
                if (!identity.Claims.Any(identityClaim => identityClaim.Type == claim.Type))
                {
                    newClaims.Add(claim);
                }
            }
            identity.AddClaims(newClaims);
        }
    }
}
