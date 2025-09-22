// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Data;
using Cite.Api.Data.Models;
using Cite.Api.Services;
using System.Threading;
using System.Threading.Tasks;

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
                    var databaseOptions = services.GetRequiredService<DatabaseOptions>();
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

                        var seedDataOptions = services.GetService<SeedDataOptions>();
                        ProcessSeedDataOptions(seedDataOptions, ctx);
                        ProcessScoringModelsBeforeTemplates(ctx);
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
            // duties
            if (options.Duties != null && options.Duties.Any())
            {
                var dbDuties = context.Duties.ToList();

                foreach (DutyEntity duty in options.Duties)
                {
                    if (!dbDuties.Where(x => x.Id == duty.Id).Any())
                    {
                        context.Duties.Add(duty);
                    }
                }
                context.SaveChanges();
            }
            // duty users
            if (options.DutyUsers != null && options.DutyUsers.Any())
            {
                var dbDutyUsers = context.DutyUsers.ToList();

                foreach (DutyUserEntity dutyUser in options.DutyUsers)
                {
                    if (!dbDutyUsers.Where(x => x.Id == dutyUser.Id).Any())
                    {
                        context.DutyUsers.Add(dutyUser);
                    }
                }
                context.SaveChanges();
            }
        }

        private static void ProcessScoringModelsBeforeTemplates(CiteContext context)
        {
            var evaluationsPointingToTemplates = context.Evaluations.Where(m => m.ScoringModel.EvaluationId == null);
            foreach (var evaluation in evaluationsPointingToTemplates)
            {
                var newScoringModelId = CopyScoringModel(context, evaluation.ScoringModelId, evaluation.CreatedBy, evaluation.DateCreated, evaluation.Id);
                evaluation.ScoringModel = null;
                evaluation.ScoringModelId = newScoringModelId;
                context.SaveChanges();
            }
        }

        private static Guid CopyScoringModel(CiteContext context, Guid scoringModelId, Guid currentUserId, DateTime dateCreated, Guid evaluationId)
        {
            var scoringModelEntity = context.ScoringModels.AsNoTracking().FirstOrDefault(m => m.Id == scoringModelId);
            scoringModelEntity.Id = Guid.NewGuid();
            scoringModelEntity.DateCreated = dateCreated;
            scoringModelEntity.CreatedBy = currentUserId;
            scoringModelEntity.DateModified = null;
            scoringModelEntity.ModifiedBy = null;
            scoringModelEntity.Description = scoringModelEntity.Description;
            scoringModelEntity.EvaluationId = evaluationId;
            var scoringCategoryIdCrossReference = new Dictionary<Guid, Guid>();
            // copy ScoringCategories
            foreach (var scoringCategory in scoringModelEntity.ScoringCategories)
            {
                var newId = Guid.NewGuid();
                scoringCategoryIdCrossReference[scoringCategory.Id] = newId;
                scoringCategory.Id = newId;
                scoringCategory.ScoringModelId = scoringModelEntity.Id;
                scoringCategory.ScoringModel = null;
                scoringCategory.DateCreated = dateCreated;
                scoringCategory.CreatedBy = currentUserId;
                // copy DataOptions
                foreach (var scoringOption in scoringCategory.ScoringOptions)
                {
                    scoringOption.Id = Guid.NewGuid();
                    scoringOption.ScoringCategoryId = scoringCategory.Id;
                    scoringOption.ScoringCategory = null;
                    scoringOption.DateCreated = dateCreated;
                    scoringOption.CreatedBy = currentUserId;
                }
            }
            context.ScoringModels.Add(scoringModelEntity);
            context.SaveChanges();

            return scoringModelEntity.Id;
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
