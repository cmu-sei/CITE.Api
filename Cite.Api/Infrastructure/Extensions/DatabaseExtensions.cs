// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Data;
using Cite.Api.Data.Models;

namespace Cite.Api.Infrastructure.Extensions
{
    public static class DatabaseExtensions
    {
        public static IHost InitializeDatabase(this IHost webHost)
        {
            using (var scope = webHost.Services.CreateScope())
            {
                var services = scope.ServiceProvider;

                try
                {
                    var databaseOptions = services.GetService<DatabaseOptions>();
                    var ctx = services.GetRequiredService<CiteContext>();

                    if (ctx != null)
                    {
                        if (databaseOptions.DevModeRecreate)
                            ctx.Database.EnsureDeleted();

                        // Do not run migrations on Sqlite, only devModeRecreate allowed
                        if (!ctx.Database.IsSqlite())
                        {
                            ctx.Database.Migrate();
                        }

                        if (databaseOptions.DevModeRecreate)
                        {
                            ctx.Database.EnsureCreated();
                        }

                        IHostEnvironment env = services.GetService<IHostEnvironment>();
                        string seedFile = Path.Combine(
                            env.ContentRootPath,
                            databaseOptions.SeedFile
                        );
                        if (File.Exists(seedFile)) {
                            SeedDataOptions seedDataOptions = JsonSerializer.Deserialize<SeedDataOptions>(File.ReadAllText(seedFile));
                            ProcessSeedDataOptions(seedDataOptions, ctx);
                        }
                    }

                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while initializing the database.");

                    // exit on database connection error on startup so app can be restarted to try again
                    throw;
                }
            }

            return webHost;
        }

        private static void ProcessSeedDataOptions(SeedDataOptions options, CiteContext context)
        {
            // permissions
            if (options.Permissions != null && options.Permissions.Any())
            {
                var dbPermissions = context.Permissions.ToList();

                foreach (PermissionEntity permission in options.Permissions)
                {
                    if (!dbPermissions.Where(x => x.Key == permission.Key && x.Value == permission.Value).Any())
                    {
                        context.Permissions.Add(permission);
                    }
                }
                context.SaveChanges();
            }
            // users
            if (options.Users != null && options.Users.Any())
            {
                var dbUsers = context.Users.ToList();

                foreach (UserEntity user in options.Users)
                {
                    if (!dbUsers.Where(x => x.Id == user.Id).Any())
                    {
                        context.Users.Add(user);
                    }
                }
                context.SaveChanges();
            }
            // user permissions
            if (options.UserPermissions != null && options.UserPermissions.Any())
            {
                var dbUserPermissions = context.UserPermissions.ToList();

                foreach (UserPermissionEntity userPermission in options.UserPermissions)
                {
                    if (!dbUserPermissions.Where(x => x.UserId == userPermission.UserId && x.PermissionId == userPermission.PermissionId).Any())
                    {
                        context.UserPermissions.Add(userPermission);
                    }
                }
                context.SaveChanges();
            }
            // scoring models
            if (options.ScoringModels != null && options.ScoringModels.Any())
            {
                var dbScoringModels = context.ScoringModels.ToList();

                foreach (ScoringModelEntity scoringModel in options.ScoringModels)
                {
                    if (!dbScoringModels.Where(x => x.Id == scoringModel.Id).Any())
                    {
                        context.ScoringModels.Add(scoringModel);
                    }
                }
                context.SaveChanges();
            }
            // scoring categories
            if (options.ScoringCategories != null && options.ScoringCategories.Any())
            {
                var dbScoringCategories = context.ScoringCategories.ToList();

                foreach (ScoringCategoryEntity scoringCategory in options.ScoringCategories)
                {
                    if (!dbScoringCategories.Where(x => x.Id == scoringCategory.Id).Any())
                    {
                        context.ScoringCategories.Add(scoringCategory);
                    }
                }
                context.SaveChanges();
            }
            // scoring options
            if (options.ScoringOptions != null && options.ScoringOptions.Any())
            {
                var dbScoringOptions = context.ScoringOptions.ToList();

                foreach (ScoringOptionEntity scoringOption in options.ScoringOptions)
                {
                    if (!dbScoringOptions.Where(x => x.Id == scoringOption.Id).Any())
                    {
                        context.ScoringOptions.Add(scoringOption);
                    }
                }
                context.SaveChanges();
            }
            // evaluations
            if (options.Evaluations != null && options.Evaluations.Any())
            {
                var dbEvaluations = context.Evaluations.ToList();

                foreach (EvaluationEntity evaluation in options.Evaluations)
                {
                    if (!dbEvaluations.Where(x => x.Id == evaluation.Id).Any())
                    {
                        context.Evaluations.Add(evaluation);
                    }
                }
                context.SaveChanges();
            }
            // team types
            if (options.TeamTypes != null && options.TeamTypes.Any())
            {
                var dbTeamTypes = context.TeamTypes.ToList();

                foreach (TeamTypeEntity teamType in options.TeamTypes)
                {
                    if (!dbTeamTypes.Where(x => x.Id == teamType.Id).Any())
                    {
                        context.TeamTypes.Add(teamType);
                    }
                }
                context.SaveChanges();
            }
            // teams
            if (options.Teams != null && options.Teams.Any())
            {
                var dbTeams = context.Teams.ToList();

                foreach (TeamEntity team in options.Teams)
                {
                    if (!dbTeams.Where(x => x.Id == team.Id).Any())
                    {
                        context.Teams.Add(team);
                    }
                }
                context.SaveChanges();
            }
            // team users
            if (options.TeamUsers != null && options.TeamUsers.Any())
            {
                var dbTeamUsers = context.TeamUsers.ToList();

                foreach (TeamUserEntity teamUser in options.TeamUsers)
                {
                    if (!dbTeamUsers.Where(x => (x.UserId == teamUser.UserId && x.TeamId == teamUser.TeamId) || x.Id == teamUser.Id).Any())
                    {
                        context.TeamUsers.Add(teamUser);
                    }
                }
                context.SaveChanges();
            }
            // moves
            if (options.Moves != null && options.Moves.Any())
            {
                var dbMoves = context.Moves.ToList();

                foreach (MoveEntity move in options.Moves)
                {
                    if (!dbMoves.Where(x => x.Id == move.Id || (x.EvaluationId == move.EvaluationId && x.MoveNumber == move.MoveNumber)).Any())
                    {
                        context.Moves.Add(move);
                    }
                }
                context.SaveChanges();
            }
            // actions
            if (options.Actions != null && options.Actions.Any())
            {
                var dbActions = context.Actions.ToList();

                foreach (ActionEntity action in options.Actions)
                {
                    if (!dbActions.Where(x => x.Id == action.Id).Any())
                    {
                        context.Actions.Add(action);
                    }
                }
                context.SaveChanges();
            }
            // roles
            if (options.Roles != null && options.Roles.Any())
            {
                var dbRoles = context.Roles.ToList();

                foreach (RoleEntity role in options.Roles)
                {
                    if (!dbRoles.Where(x => x.Id == role.Id).Any())
                    {
                        context.Roles.Add(role);
                    }
                }
                context.SaveChanges();
            }
            // role users
            if (options.RoleUsers != null && options.RoleUsers.Any())
            {
                var dbRoleUsers = context.RoleUsers.ToList();

                foreach (RoleUserEntity roleUser in options.RoleUsers)
                {
                    if (!dbRoleUsers.Where(x => x.Id == roleUser.Id).Any())
                    {
                        context.RoleUsers.Add(roleUser);
                    }
                }
                context.SaveChanges();
            }
        }

        private static string DbProvider(IConfiguration config)
        {
            return config.GetValue<string>("Database:Provider", "Sqlite").Trim();
        }

        public static DbContextOptionsBuilder UseConfiguredDatabase(
            this DbContextOptionsBuilder builder,
            IConfiguration config
        )
        {
            string dbProvider = DbProvider(config);
            var migrationsAssembly = String.Format("{0}.Migrations.{1}", typeof(Startup).GetTypeInfo().Assembly.GetName().Name, dbProvider);
            var connectionString = config.GetConnectionString(dbProvider);

            switch (dbProvider)
            {
                case "Sqlite":
                    builder.UseSqlite(connectionString, options => options.MigrationsAssembly(migrationsAssembly));
                    break;

                case "PostgreSQL":
                    builder.UseNpgsql(connectionString, options => {
                        options.MigrationsAssembly(migrationsAssembly);
                        options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    });
                    // builder.ConfigureWarnings(w => w.Throw(RelationalEventId.MultipleCollectionIncludeWarning));
                    break;

            }
            return builder;
        }
    }
}
