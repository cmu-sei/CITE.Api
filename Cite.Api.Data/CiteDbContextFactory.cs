// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.
using System;
using Microsoft.EntityFrameworkCore;
namespace Cite.Api.Data;
public class CiteContextFactory : IDbContextFactory<CiteContext>
{
    private readonly IDbContextFactory<CiteContext> _pooledFactory;
    private readonly IServiceProvider _serviceProvider;
    public CiteContextFactory(
        IDbContextFactory<CiteContext> pooledFactory,
        IServiceProvider serviceProvider)
    {
        _pooledFactory = pooledFactory;
        _serviceProvider = serviceProvider;
    }
    public CiteContext CreateDbContext()
    {
        var context = _pooledFactory.CreateDbContext();
        // Inject the current scope's ServiceProvider
        context.ServiceProvider = _serviceProvider;
        return context;
    }
}