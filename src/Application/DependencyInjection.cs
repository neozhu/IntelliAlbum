// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Common.Behaviours;
using CleanArchitecture.Blazor.Application.Common.Interfaces.MultiTenant;
using CleanArchitecture.Blazor.Application.Common.PublishStrategies;
using CleanArchitecture.Blazor.Application.Common.Security;
using  CleanArchitecture.Blazor.Application.BackendServices;
using CleanArchitecture.Blazor.Application.Services.MultiTenant;
using CleanArchitecture.Blazor.Application.Services.Picklist;
using Microsoft.Extensions.DependencyInjection;
using CleanArchitecture.Blazor.Application.Services.BackendServices;

namespace CleanArchitecture.Blazor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddMediatR(config=> {
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            config.NotificationPublisher = new ParallelNoWaitPublisher();
            config.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
            config.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
            config.AddOpenBehavior(typeof(RequestExceptionProcessorBehavior<,>));
            config.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            config.AddOpenBehavior(typeof(MemoryCacheBehaviour<,>));
            config.AddOpenBehavior(typeof(AuthorizationBehaviour<,>));
            config.AddOpenBehavior(typeof(CacheInvalidationBehaviour<,>));
         
            
        });
        services.AddFluxor(options => {
            options.ScanAssemblies(Assembly.GetExecutingAssembly());
            options.UseReduxDevTools();
        });
        services.AddLazyCache();
        services.AddScoped<PicklistService>();
        services.AddScoped<IPicklistService>(sp => {
            var service = sp.GetRequiredService<PicklistService>();
            service.Initialize();
            return service;
            });
        services.AddScoped<TenantService>();
        services.AddScoped<ITenantService>(sp => {
            var service = sp.GetRequiredService<TenantService>();
            service.Initialize();
            return service;
        });
        services.AddScoped<RegisterFormModelFluentValidator>();

        services.AddImageServices();
        services.AddSingletonBackEndServices();
        return services;
    }
    public static IServiceCollection AddSingletonBackEndServices(this IServiceCollection services)
    {
        //services.AddSingleton<StatisticsService>();
        //services.AddSingleton<ConfigService>();
        services.AddSingleton<YoloAIService>();
        services.AddSingleton<ObjectDetectService>();
        services.AddSingleton<FolderWatcherService>();
        services.AddSingleton<IndexingService>();
        services.AddSingleton<MetaDataService>();
        services.AddSingleton<ThumbnailService>();
        //services.AddSingleton<ExifService>();
        services.AddSingleton<FolderService>();
        //services.AddSingleton<ThemeService>();
        //services.AddSingleton<ImageRecognitionService>();
        services.AddSingleton<ImageCache>();
        services.AddSingleton<WorkService>();
        //services.AddSingleton<CachedDataService>();
        //services.AddSingleton<TaskService>();
        //services.AddSingleton<RescanService>();
        services.AddSingleton<ServerNotifierService>();
        services.AddSingleton<ServerStatusService>();
        //services.AddSingleton<DownloadService>();

        services.AddSingleton<IImageCacheService>(x => x.GetRequiredService<ImageCache>());
        services.AddSingleton<IStatusService>(x => x.GetRequiredService<ServerStatusService>());
        services.AddSingleton<IFolderService>(x => x.GetRequiredService<FolderService>());
        services.AddSingleton<IWorkService>(x => x.GetRequiredService<WorkService>());




        return services;
    }
    public static IServiceCollection AddImageServices(this IServiceCollection services)
    {
        return services.AddSingleton<ImageProcessorFactory>()
            .AddSingleton<IImageProcessorFactory>(x => x.GetRequiredService<ImageProcessorFactory>())
            .AddSingleton<ImageProcessService>();
    }
}
