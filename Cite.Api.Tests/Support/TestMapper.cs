// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

// cite.api: Startup's AddAutoMapper scans the Cite.Api assembly and applies one global convention, a
// member resolver for nullable sources mapped onto non-nullable destinations. The resolver
// (Cite.Api.Infrastructure.Mapping.IgnoreNullSourceValues) is internal, so it is copied here privately;
// MappingConfigurationTests guards the copy.

using AutoMapper;
using AutoMapper.Internal;

namespace Cite.Api.Tests.Support;

/// <summary>The application's real AutoMapper configuration, built without starting the application.</summary>
public static class TestMapper
{
    private static readonly Lazy<MapperConfiguration> LazyConfiguration = new(() =>
        new MapperConfiguration(cfg =>
        {
            cfg.Internal().ForAllPropertyMaps(
                pm => pm.SourceType != null && Nullable.GetUnderlyingType(pm.SourceType) == pm.DestinationType,
                (pm, c) => c.MapFrom<object, object, object, object>(new IgnoreNullSourceValues(), pm.SourceMember.Name));
            cfg.AddMaps(typeof(Cite.Api.Startup).Assembly);
        }));

    /// <summary>The shared configuration, built once for the run.</summary>
    public static MapperConfiguration Configuration => LazyConfiguration.Value;

    /// <summary>A mapper over <see cref="Configuration"/>. Thread-safe; tests share one.</summary>
    public static IMapper Mapper => LazyMapper.Value;

    private static readonly Lazy<IMapper> LazyMapper = new(() => LazyConfiguration.Value.CreateMapper());

    /// <summary>The copy of <c>Cite.Api.Infrastructure.Mapping.IgnoreNullSourceValues</c>.</summary>
    private sealed class IgnoreNullSourceValues : IMemberValueResolver<object, object, object, object>
    {
        public object Resolve(object source, object destination, object sourceMember, object destinationMember, ResolutionContext context) =>
            sourceMember ?? destinationMember;
    }
}
