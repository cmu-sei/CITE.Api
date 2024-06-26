// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Cite.Api.Infrastructure.EventHandlers;
using Cite.Api.Infrastructure.Extensions;
using Cite.Api.Data;
using Cite.Api.Infrastructure.JsonConverters;
using Cite.Api.Infrastructure.Mapping;
using Cite.Api.Infrastructure.Options;
using Cite.Api.Services;
using System;
using MediatR;
using Cite.Api.Infrastructure;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Infrastructure.Filters;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Principal;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Cite.Api
{
    public class Startup
    {
        public Infrastructure.Options.AuthorizationOptions _authOptions = new Infrastructure.Options.AuthorizationOptions();
        public Infrastructure.Options.XApiOptions _xApiOptions = new Infrastructure.Options.XApiOptions();
        public IConfiguration Configuration { get; }
        private string _pathbase;
        private const string _routePrefix = "api";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            Configuration.GetSection("Authorization").Bind(_authOptions);
            Configuration.GetSection("XApiOptions").Bind(_xApiOptions);
            _pathbase = Configuration["PathBase"];
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add Azure Application Insights, if connection string is supplied
            string appInsights = Configuration["ApplicationInsights:ConnectionString"];
            if (!string.IsNullOrWhiteSpace(appInsights))
            {
                services.AddApplicationInsightsTelemetry();
            }

            var provider = Configuration["Database:Provider"];
            var connectionString = Configuration.GetConnectionString(provider);
            switch (provider)
            {
                case "InMemory":
                    services.AddDbContextPool<CiteContext>((serviceProvider, optionsBuilder) => optionsBuilder
                        .AddInterceptors(serviceProvider.GetRequiredService<EntityTransactionInterceptor>())
                        .UseInMemoryDatabase("api"));
                    break;
                case "Sqlite":
                    services.AddDbContextPool<CiteContext>((serviceProvider, optionsBuilder) => optionsBuilder
                        .AddInterceptors(serviceProvider.GetRequiredService<EntityTransactionInterceptor>())
                        .UseConfiguredDatabase(Configuration))
                        .AddHealthChecks().AddSqlite(connectionString, tags: new[] { "ready", "live"});
                    break;
                case "SqlServer":
                    services.AddDbContextPool<CiteContext>((serviceProvider, optionsBuilder) => optionsBuilder
                        .AddInterceptors(serviceProvider.GetRequiredService<EntityTransactionInterceptor>())
                        .UseConfiguredDatabase(Configuration))
                        .AddHealthChecks().AddSqlServer(connectionString, tags: new[] { "ready", "live"});
                    break;
                case "PostgreSQL":
                    services.AddDbContextPool<CiteContext>((serviceProvider, optionsBuilder) => optionsBuilder
                        .AddInterceptors(serviceProvider.GetRequiredService<EntityTransactionInterceptor>())
                        .UseConfiguredDatabase(Configuration))
                        .AddHealthChecks().AddNpgSql(connectionString, tags: new[] { "ready", "live"});
                    break;
            }

            services.AddOptions()
                .Configure<DatabaseOptions>(Configuration.GetSection("Database"))
                    .AddScoped(config => config.GetService<IOptionsMonitor<DatabaseOptions>>().CurrentValue)

                .Configure<XApiOptions>(Configuration.GetSection("XApiOptions"))
                    .AddScoped(config => config.GetService<IOptionsMonitor<XApiOptions>>().CurrentValue)

                .Configure<ClaimsTransformationOptions>(Configuration.GetSection("ClaimsTransformation"))
                    .AddScoped(config => config.GetService<IOptionsMonitor<ClaimsTransformationOptions>>().CurrentValue)

                .Configure<SeedDataOptions>(Configuration.GetSection("SeedData"))
                    .AddScoped(config => config.GetService<IOptionsMonitor<SeedDataOptions>>().CurrentValue);

            services
                .Configure<ClientOptions>(Configuration.GetSection("ClientSettings"))
                    .AddScoped(config => config.GetService<IOptionsMonitor<ClientOptions>>().CurrentValue);

            services.AddScoped<IClaimsTransformation, AuthorizationClaimsTransformer>();
            services.AddScoped<IUserClaimsService, UserClaimsService>();

            services.AddCors(options => options.UseConfiguredCors(Configuration.GetSection("CorsPolicy")));

            services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            services.AddMvc(options =>
            {
                options.Filters.Add(typeof(ValidateModelStateFilter));
                options.Filters.Add(typeof(JsonExceptionFilter));

                // Require all scopes in authOptions
                var policyBuilder = new AuthorizationPolicyBuilder().RequireAuthenticatedUser();
                Array.ForEach(_authOptions.AuthorizationScope.Split(' '), x => policyBuilder.RequireScope(x));

                var policy = policyBuilder.Build();
                options.Filters.Add(new AuthorizeFilter(policy));
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonNullableGuidConverter());
                options.JsonSerializerOptions.Converters.Add(new JsonDoubleConverter());
                options.JsonSerializerOptions.Converters.Add(new JsonIntegerConverter());
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            services.AddSwagger(_authOptions);

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = _authOptions.Authority;
                options.RequireHttpsMetadata = _authOptions.RequireHttpsMetadata;
                options.SaveToken = true;
                string[] validAudiences;
                if (_authOptions.ValidAudiences != null && _authOptions.ValidAudiences.Any())
                {
                    validAudiences = _authOptions.ValidAudiences;
                }
                else
                {
                    validAudiences = _authOptions.AuthorizationScope.Split(' ');
                }

                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateAudience = _authOptions.ValidateAudience,
                    ValidAudiences = validAudiences
                };
            });

            services.AddRouting(options =>
            {
                options.LowercaseUrls = true;
            });

            services.AddMemoryCache();

            services.AddScoped<IActionService, ActionService>();
            services.AddScoped<IEvaluationService, EvaluationService>();
            services.AddScoped<IGalleryService, GalleryService>();
            services.AddScoped<IMoveService, MoveService>();
            services.AddScoped<IXApiService, XApiService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IScoringCategoryService, ScoringCategoryService>();
            services.AddScoped<IScoringModelService, ScoringModelService>();
            services.AddScoped<IScoringOptionService, ScoringOptionService>();
            services.AddScoped<ISubmissionCategoryService, SubmissionCategoryService>();
            services.AddScoped<ISubmissionCommentService, SubmissionCommentService>();
            services.AddScoped<ISubmissionService, SubmissionService>();
            services.AddScoped<ISubmissionOptionService, SubmissionOptionService>();
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<ITeamService, TeamService>();
            services.AddScoped<ITeamTypeService, TeamTypeService>();
            services.AddScoped<ITeamUserService, TeamUserService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUserPermissionService, UserPermissionService>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<IPrincipal>(p => p.GetService<IHttpContextAccessor>().HttpContext.User);
            services.AddHttpClient();

            ApplyPolicies(services);

            services.AddTransient<EntityTransactionInterceptor>();
            services.AddAutoMapper(cfg =>
            {
                cfg.ForAllPropertyMaps(
                    pm => pm.SourceType != null && Nullable.GetUnderlyingType(pm.SourceType) == pm.DestinationType,
                    (pm, c) => c.MapFrom<object, object, object, object>(new IgnoreNullSourceValues(), pm.SourceMember.Name));
            }, typeof(Startup));
            services.AddMediatR(typeof(Startup));
            services
                .Configure<ResourceOwnerAuthorizationOptions>(Configuration.GetSection("ResourceOwnerAuthorization"))
                .AddScoped(config => config.GetService<IOptionsMonitor<ResourceOwnerAuthorizationOptions>>().CurrentValue);

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UsePathBase(_pathbase);
            app.UseRouting();
            app.UseCors("default");

            //move any querystring jwt to Auth bearer header
            app.Use(async (context, next) =>
            {
                if (string.IsNullOrWhiteSpace(context.Request.Headers["Authorization"])
                    && context.Request.QueryString.HasValue)
                {
                    string token = context.Request.QueryString.Value
                        .Substring(1)
                        .Split('&')
                        .SingleOrDefault(x => x.StartsWith("bearer="))?.Split('=')[1];

                    if (!String.IsNullOrWhiteSpace(token))
                        context.Request.Headers.Add("Authorization", new[] { $"Bearer {token}" });
                }

                await next.Invoke();

            });

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.RoutePrefix = _routePrefix;
                c.SwaggerEndpoint($"{_pathbase}/swagger/v1/swagger.json", "Cite v1");
                c.OAuthClientId(_authOptions.ClientId);
                c.OAuthClientSecret(_authOptions.ClientSecret);
                c.OAuthAppName(_authOptions.ClientName);
                c.OAuthUsePkce();
            });

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapHub<Hubs.MainHub>("/hubs/main");
                    endpoints.MapHealthChecks($"/{_routePrefix}/health/ready", new HealthCheckOptions()
                    {
                        Predicate = (check) => check.Tags.Contains("ready"),
                    });

                    endpoints.MapHealthChecks($"/{_routePrefix}/health/live", new HealthCheckOptions()
                    {
                        Predicate = (check) => check.Tags.Contains("live"),
                    });
                }
            );

            app.UseHttpContext();
        }


        private void ApplyPolicies(IServiceCollection services)
        {
            services.AddAuthorizationPolicy(_authOptions);
        }
    }
}
