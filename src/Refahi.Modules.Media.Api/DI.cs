using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Refahi.Modules.Media.Application;
using Refahi.Modules.Media.Infrastructure;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Media.Api;

public static class DI
{
    public static IServiceCollection RegisterMediaModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services
            .RegisterApplication(configuration)
            .RegisterInfrastructure(configuration);

        // محدودیت‌های Multipart برای پشتیبانی از batch upload
        services.Configure<FormOptions>(o =>
        {
            o.MultipartBodyLengthLimit = MediaConstants.MaxBatchBodyBytes;
            o.ValueCountLimit = 1024;
        });

        return services;
    }

    public static WebApplication UseMediaModule(this WebApplication app, string endPointsPrefix)
    {
        // اعمال migrations (الگوی استاندارد ماژول‌ها)
        app.Services.UseInfrastructure(app.Environment.IsDevelopment());

        // ۱. سرو فایل‌های مدیا — مسیر بیرون از /api تا ResponseWrappingMiddleware آن را skip کند
        var basePath = app.Configuration["MediaStorage:BasePath"];
        if (!string.IsNullOrWhiteSpace(basePath))
        {
            Directory.CreateDirectory(basePath);

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(basePath),
                RequestPath = "/media-files",
                ServeUnknownFileTypes = false,
                ContentTypeProvider = new FileExtensionContentTypeProvider(),
                OnPrepareResponse = ctx =>
                {
                    // یک سال cache برای فایل‌های public (نام Guid → immutable)
                    ctx.Context.Response.Headers.CacheControl =
                        "public,max-age=31536000,immutable";
                    ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                }
            });
        }

        // ۲. ثبت Endpoints (الگوی استاندارد reflection-based)
        MapEndPoints(app, endPointsPrefix);

        return app;
    }

    private static void MapEndPoints(WebApplication app, string endPointsPrefix)
    {
        var assembly = typeof(DI).Assembly;

        var endpointTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IEndpoint).IsAssignableFrom(t));

        var group = app.MapGroup(endPointsPrefix);

        foreach (var type in endpointTypes)
        {
            if (Activator.CreateInstance(type) is IEndpoint endpoint)
                endpoint.Map(group);
        }

        group.MapGet("/ping", () => Results.Ok(new { module = "Media Module" }));
    }
}
