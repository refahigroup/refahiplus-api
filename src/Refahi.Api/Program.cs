using MediatR;
using Microsoft.OpenApi;
using Microsoft.Extensions.Configuration;
using Refahi.Api.Services.Notification;
using Refahi.Modules.Catalog.Api;
using Refahi.Modules.Hotels.Api;
using Refahi.Modules.Identity.Api;
using Refahi.Modules.Orders.Api;
using Refahi.Modules.Organizations.Api;
using Refahi.Modules.Wallets.Api;
using Refahi.Api.Services.Chaching;
using Refahi.Api;

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

//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("v1/swagger.json", "Refahi API v1");
        options.RoutePrefix = "api/swagger";
    });
//}

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));

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
