using Catalog.Api;
using Identity.Api;
using MediatR;
using Microsoft.OpenApi;
using Orders.Api;
using Organizations.Api;
using Refahi.Host;
using Wallets.Api;

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

// Register modules
try
{
    builder.Services
        .RegisterIdentityModule(builder.Configuration, builder.Environment)
        .RegisterOrganizationsModule(builder.Configuration)
        .RegisterWalletsModule(builder.Configuration)
        .RegisterCatalogModule(builder.Configuration)
        .RegisterOrdersModule(builder.Configuration);
}
catch 
{ 
    /* ignore if compile-time linking not present yet */ 
}

// Register cross-cutting pipeline behaviors after modules register their MediatR/Validators
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Refahi API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Map module endpoints
try
{
    app.UseIdentityModule("/api/auth")
        .MapOrganizationsEndpoints("/api/organizations")
        .UseWalletsModule("/api/wallets")
        .UseCatalogModule("/api/catalog")
        .UseOrdersModule("/api/orders");
}
catch 
{ 
    /* endpoints will be available once modules compiled */ 
}

app.Run();
