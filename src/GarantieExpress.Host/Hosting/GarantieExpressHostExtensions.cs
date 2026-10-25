using GarantieExpress.Application.DependencyInjection;
using GarantieExpress.Host.Components;
using GarantieExpress.Host.Updates;
using GarantieExpress.Infrastructure.DependencyInjection;
using GarantieExpress.UI.Abstractions;
using GarantieExpress.UI.DependencyInjection;

namespace GarantieExpress.Host.Hosting;

internal static class GarantieExpressHostExtensions
{
    public static WebApplicationBuilder AddGarantieExpressHost(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        var settings = LocalHostSettings.FromConfiguration(builder.Configuration);
        var installedVersion = typeof(Program).Assembly.GetName().Version
            ?? throw new InvalidOperationException("The application version is unavailable.");
        var updateOptions = UpdateCheckOptions.FromConfiguration(
            builder.Configuration,
            installedVersion);
        builder.WebHost.UseUrls(settings.Endpoint.AbsoluteUri);

        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services
            .AddGarantieExpressApplication()
            .AddGarantieExpressInfrastructure(
                builder.Configuration["GarantieExpress:DataDirectory"])
            .AddGarantieExpressUI()
            .AddLocalApplicationHost(settings, updateOptions);

        return builder;
    }

    private static IServiceCollection AddLocalApplicationHost(
        this IServiceCollection services,
        LocalHostSettings settings,
        UpdateCheckOptions updateOptions)
    {
        services.AddSingleton(settings);
        services.AddSingleton(updateOptions);
        services.AddHttpClient("GarantieExpress.Updates", client =>
            client.MaxResponseContentBufferSize = 16 * 1024);
        services.AddSingleton<ApplicationUpdateService>();
        services.AddSingleton<SingleInstanceGuard>();
        services.AddSingleton<BrowserLauncher>();
        services.AddSingleton<ILocalApplicationLifetime, LocalApplicationLifetime>();
        services.AddHostedService<LocalApplicationHostedService>();
        services.AddHostedService<ApplicationUpdateMonitoringService>();
        services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = TimeSpan.FromSeconds(10));
        return services;
    }

    public static WebApplication UseGarantieExpressHost(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var settings = app.Services.GetRequiredService<LocalHostSettings>();

        if (!app.Environment.IsDevelopment())
            app.UseExceptionHandler("/Error", createScopeForErrors: true);

        app.UseLocalRequestPolicy(settings.Endpoint);
        app.UseWebSockets(new WebSocketOptions
        {
            AllowedOrigins = { settings.Endpoint.GetLeftPart(UriPartial.Authority) }
        });
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(typeof(GarantieExpress.UI.Pages.Home).Assembly);

        return app;
    }

    public static async Task<int> RunGarantieExpressAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        // RunAsync owns disposal for the primary instance; early secondary exits do not.
        var disposeApp = true;
        try
        {
            var guard = app.Services.GetRequiredService<SingleInstanceGuard>();
            var result = await guard.AcquireAsync(cancellationToken);
            if (result is SingleInstanceResult.Primary)
            {
                disposeApp = false;
                await app.RunAsync(cancellationToken);
                return 0;
            }

            if (result is SingleInstanceResult.Secondary secondary)
            {
                app.Services
                    .GetRequiredService<BrowserLauncher>()
                    .Open(secondary.Endpoint);
                return 0;
            }

            app.Logger.LogError(
                "Another GarantieExpress process is running, but its local endpoint is unavailable.");
            return 1;
        }
        finally
        {
            if (disposeApp) await app.DisposeAsync();
        }
    }

    private static void UseLocalRequestPolicy(this WebApplication app, Uri endpoint)
    {
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                if (context.Response.ContentType?.StartsWith(
                        "text/html",
                        StringComparison.OrdinalIgnoreCase) == true)
                {
                    context.Response.Headers.CacheControl = "no-cache";
                }

                context.Response.Headers.XContentTypeOptions = "nosniff";
                return Task.CompletedTask;
            });

            var requestPort = context.Request.Host.Port
                ?? (context.Request.IsHttps ? 443 : 80);
            if (!string.Equals(
                    context.Request.Host.Host,
                    endpoint.Host,
                    StringComparison.OrdinalIgnoreCase)
                || requestPort != endpoint.Port
                || !string.Equals(
                    context.Request.Scheme,
                    endpoint.Scheme,
                    StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            await next(context);
        });
    }
}
