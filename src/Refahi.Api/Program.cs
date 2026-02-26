using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi;
using Refahi.Api;
using Refahi.Api.Middlewares;
using Refahi.Api.Services.Chaching;
using Refahi.Api.Services.Notification;
using Refahi.Modules.Catalog.Api;
using Refahi.Modules.Hotels.Api;
using Refahi.Modules.Identity.Api;
using Refahi.Modules.Orders.Api;
using Refahi.Modules.Organizations.Api;
using Refahi.Modules.Wallets.Api;
using System.Diagnostics;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Refahi API",
        Version = "v1",
        Description = "Refahi - Modular Monolith API with Clean Architecture & DDD"
    });
});


// Register Shared services
builder.Services
    .RegisterCachingService(builder.Configuration, builder.Environment.IsDevelopment())
    .RegisterNotificationService(builder.Configuration);

// Register modules
//try
//{
    builder.Services
        .RegisterIdentityModule(builder.Configuration, builder.Environment)
        .RegisterOrganizationsModule(builder.Configuration)
        .RegisterWalletsModule(builder.Configuration)
        .RegisterCatalogModule(builder.Configuration)
        .RegisterOrdersModule(builder.Configuration)
        .RegisterHotelsModule(builder.Configuration);
//}
//catch(Exception ex) 
//{
    /* ignore if compile-time linking not present yet */
//    int a = 0;
//}

// Register cross-cutting pipeline behaviors after modules register their MediatR/Validators
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

var app = builder.Build();


// Add global exception handling middleware (must be first)
app.UseApiExceptionMiddleware();

//// Add response wrapping middleware
//// Add response wrapping middleware (wrap API JSON responses)
//// Registered after exception middleware to ensure errors are handled first
app.UseResponseWrappingMiddleware();

//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "api/swagger/{documentName}/swagger.json";
    });

    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "api/swagger";
        options.SwaggerEndpoint("v1/swagger.json", "Refahi API v1");
    });
//}

app.MapGet("/api/health", () => {

    var assembly = Assembly.GetExecutingAssembly();
    var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

    return Results.Ok(new
    {
        status = "healthy",
        ver = fvi.FileVersion
    });
});

// Map module endpoints
//try
//{
    app.UseIdentityModule("/api/auth")
       .MapOrganizationsEndpoints("/api/organizations")
       .UseWalletsModule("/api/wallets")
       .UseCatalogModule("/api/catalog")
       .UseOrdersModule("/api/orders")
       .UseHotelModule("/api/hotels");
//}
//catch 
//{ 
    /* endpoints will be available once modules compiled */ 
//}

app.Run();
