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
using Microsoft.Extensions.Logging;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Infrastructure.Exceptions;
using Cite.Api.ViewModels;

namespace Cite.Api.Services
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetAsync(CancellationToken ct);
        Task<IEnumerable<UserIdentity>> GetByEvaluationAsync(Guid evaluationId, bool hasSystemPermission, CancellationToken ct);
        Task<User> GetAsync(Guid id, CancellationToken ct);
        Task<User> CreateAsync(User user, CancellationToken ct);
        Task<User> UpdateAsync(Guid id, User user, CancellationToken ct);
        Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    }

    public class UserService : IUserService
    {
        private readonly CiteContext _context;
        private readonly ClaimsPrincipal _user;
        private readonly IAuthorizationService _authorizationService;
        private readonly IUserClaimsService _userClaimsService;
        private readonly IMapper _mapper;
        private readonly ILogger<IUserService> _logger;

        public UserService(CiteContext context, IPrincipal user, IAuthorizationService authorizationService, IUserClaimsService userClaimsService, ILogger<IUserService> logger, IMapper mapper)
        {
            _context = context;
            _user = user as ClaimsPrincipal;
            _authorizationService = authorizationService;
            _userClaimsService = userClaimsService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<User>> GetAsync(CancellationToken ct)
        {
            var items = await _context.Users
                .ToArrayAsync(ct);
            return _mapper.Map<IEnumerable<User>>(items);
        }

        public async Task<IEnumerable<UserIdentity>> GetByEvaluationAsync(Guid evaluationId, bool hasSystemPermission, CancellationToken ct)
        {
            var items = await _context.TeamMemberships
                .Where(em => em.Team.EvaluationId == evaluationId)
                .Select(em => new UserIdentity
                {
                    Id = em.User.Id,
                    Name = em.User.Name,
                })
                .ToArrayAsync(ct);
            var userId = _user.GetId();
            if (!hasSystemPermission && !items.Any(m => m.Id == userId))
                throw new ForbiddenException();

            return _mapper.Map<IEnumerable<UserIdentity>>(items);
        }

        public async Task<User> GetAsync(Guid id, CancellationToken ct)
        {
            var item = await _context.Users
                .SingleOrDefaultAsync(o => o.Id == id, ct);
            return _mapper.Map<User>(item);
        }

        public async Task<User> CreateAsync(User user, CancellationToken ct)
        {
            var userEntity = _mapper.Map<UserEntity>(user);

            _context.Users.Add(userEntity);
            await _context.SaveChangesAsync(ct);
            _logger.LogWarning($"User {user.Name} ({userEntity.Id}) created by {_user.GetId()}");
            return await GetAsync(user.Id, ct);
        }

        public async Task<User> UpdateAsync(Guid id, User user, CancellationToken ct)
        {
            // Don't allow changing your own Id
            if (id == _user.GetId() && id != user.Id)
            {
                throw new ForbiddenException("You cannot change your own Id");
            }

            var userToUpdate = await _context.Users.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (userToUpdate == null)
                throw new EntityNotFoundException<User>();

            _mapper.Map(user, userToUpdate);

            _context.Users.Update(userToUpdate);
            await _context.SaveChangesAsync(ct);
            _logger.LogWarning($"User {userToUpdate.Name} ({userToUpdate.Id}) updated by {_user.GetId()}");
            return await GetAsync(id, ct);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
        {
            if (id == _user.GetId())
            {
                throw new ForbiddenException("You cannot delete your own account");
            }

            var userToDelete = await _context.Users.SingleOrDefaultAsync(v => v.Id == id, ct);

            if (userToDelete == null)
                throw new EntityNotFoundException<User>();

            _context.Users.Remove(userToDelete);
            await _context.SaveChangesAsync(ct);
            _logger.LogWarning($"User {userToDelete.Name} ({userToDelete.Id}) deleted by {_user.GetId()}");
            return true;
        }

    }
}

public class UserIdentity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}