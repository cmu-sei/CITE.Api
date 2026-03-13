// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using Cite.Api.Data;
using Cite.Api.Data.Enumerations;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.ViewModels;
using Crucible.Common.Testing.Auth;
using Crucible.Common.Testing.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Cite.Api.Tests.Integration.Fixtures;

public class CiteTestContext : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseEnvironment("Test")
            .UseSetting("Database:Provider", "PostgreSQL")
            .UseSetting("Authorization:Authority", "https://test-authority")
            .UseSetting("Authorization:AuthorizationScope", "cite-api")
            .UseSetting("Authorization:ClientId", "test-client")
            .ConfigureServices(services =>
            {
                if (_container is null)
                {
                    throw new InvalidOperationException(
                        "Cannot initialize CiteTestContext - the database container has not been started.");
                }

                var connectionString = _container.GetConnectionString();

                // Remove existing DbContext registrations
                services.RemoveServices<DbContextOptions<CiteContext>>();
                services.RemoveServices<CiteContext>();

                // Remove the EventPublishingDbContextFactory registration and replace with a direct DbContext
                var factoryDescriptors = services
                    .Where(d => d.ServiceType.IsGenericType &&
                                d.ServiceType.GetGenericTypeDefinition().Name.Contains("IDbContextFactory"))
                    .ToList();
                foreach (var descriptor in factoryDescriptors)
                    services.Remove(descriptor);

                services.AddDbContext<CiteContext>((serviceProvider, options) =>
                {
                    options.UseNpgsql(connectionString);
                });

                // Replace authentication with test handler
                services.AddAuthentication(TestAuthenticationHandler.AuthenticationSchemeName)
                    .AddScheme<TestAuthenticationHandlerOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.AuthenticationSchemeName, _ => { });

                // Replace claims transformation
                services.ReplaceService<IClaimsTransformation, TestClaimsTransformation>(allowMultipleReplace: true);

                // Replace authorization service to allow all
                services.ReplaceService<IAuthorizationService, TestAuthorizationService>();

                // Replace the custom Cite authorization service to allow all
                services.ReplaceService<ICiteAuthorizationService, TestCiteAuthorizationService>();
            });
    }

    public CiteContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<CiteContext>();
    }

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithHostname("localhost")
            .WithUsername("foundry")
            .WithPassword("foundry")
            .WithImage("postgres:latest")
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

        // Ensure the database is created
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CiteContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

/// <summary>
/// A test implementation of ICiteAuthorizationService that allows all operations.
/// </summary>
public class TestCiteAuthorizationService : ICiteAuthorizationService
{
    public Task<bool> AuthorizeAsync(
        SystemPermission[] requiredSystemPermissions,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public Task<bool> AuthorizeAsync<T>(
        Guid? resourceId,
        SystemPermission[] requiredSystemPermissions,
        EvaluationPermission[] requiredEvaluationPermissions,
        CancellationToken cancellationToken) where T : IAuthorizationType
    {
        return Task.FromResult(true);
    }

    public Task<bool> AuthorizeAsync<T>(
        Guid? resourceId,
        SystemPermission[] requiredSystemPermissions,
        ScoringModelPermission[] requiredScoringModelPermissions,
        CancellationToken cancellationToken) where T : IAuthorizationType
    {
        return Task.FromResult(true);
    }

    public Task<bool> AuthorizeAsync<T>(
        Guid? resourceId,
        SystemPermission[] requiredSystemPermissions,
        TeamPermission[] requiredTeamPermissions,
        CancellationToken cancellationToken) where T : IAuthorizationType
    {
        return Task.FromResult(true);
    }

    public IEnumerable<Guid> GetAuthorizedEvaluationIds() => [];

    public IEnumerable<SystemPermission> GetSystemPermissions() =>
        Enum.GetValues<SystemPermission>();

    public IEnumerable<EvaluationPermissionClaim> GetEvaluationPermissions(Guid? evaluationId = null) => [];

    public IEnumerable<ScoringModelPermissionClaim> GetScoringModelPermissions(Guid? scoringModelId = null) => [];

    public IEnumerable<TeamPermissionClaim> GetTeamPermissions(Guid? evaluationId = null) => [];
}
