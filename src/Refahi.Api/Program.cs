using MediatR;
using Microsoft.OpenApi;
using Refahi.Api;
using Refahi.Api.Middlewares;
using Refahi.Api.Services;
using Refahi.Api.Services.Chaching;
using Refahi.Api.Services.Notification;
using Refahi.Api.Services.Path;
using Refahi.Modules.Hotels.Api;
using Refahi.Modules.Identity.Api;
using Refahi.Modules.Media.Api;
using Refahi.Modules.Orders.Api;
using Refahi.Modules.Organizations.Api;
using Refahi.Modules.References.Api;
using Refahi.Modules.Store.Api;
using Refahi.Modules.SupplyChain.Api;
using Refahi.Modules.Wallets.Api;
using System.Diagnostics;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// تنظیم Kestrel و IIS برای پشتیبانی از آپلود فایل‌های بزرگ (ویدیو تا ۲۰۰MB، batch تا ۱GB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MediaConstants.MaxBatchBodyBytes;
});
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = MediaConstants.MaxBatchBodyBytes;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Refahi API",
        Version = "v1",
        Description = "Refahi Plus API"
    });

    // JWT Bearer authentication for Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "توکن JWT را وارد کنید. مثال: eyJhbGciOiJIUzI1NiIs..."
    });

    // Global security requirement — Swagger UI sends Bearer header for all operations
    // Must pass `doc` so the reference serializes correctly as {"Bearer": []} not {}
    options.AddSecurityRequirement(doc =>
        new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference("Bearer", doc), new List<string>() }
        });
});


builder.Services.RegisterDbTools();

// Register Shared services
builder.Services
    .RegisterCachingService(builder.Configuration, builder.Environment.IsDevelopment())
    .RegisterNotificationService(builder.Configuration)
    .RegisterPathService(builder.Configuration);


// Register modules
//try
//{
    builder.Services
        .RegisterReferencesModule(builder.Configuration)
        .RegisterMediaModule(builder.Configuration)
        .RegisterIdentityModule(builder.Configuration)
        .RegisterOrganizationsModule(builder.Configuration)
        .RegisterWalletsModule(builder.Configuration)
        .RegisterOrdersModule(builder.Configuration)
        .RegisterHotelsModule(builder.Configuration)
        .RegisterStoreModule(builder.Configuration)
        .RegisterSupplyChainModule(builder.Configuration);
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

app.UseAuthentication();
app.UseAuthorization();

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
    app.UseReferencesModule("/api/references")
       .UseMediaModule("/api/media")
       .UseIdentityModule("/api/auth")
       .UseOrganizationsModule("/api/organizations")
       .UseWalletsModule("/api/wallets")
       .UseOrdersModule("/api/orders")
       .UseHotelModule("/api/hotels")
       .UseStoreModule("/api/store")
       .UseSupplyChainModule("/api/supply-chain");
//}
//catch 
//{ 
    /* endpoints will be available once modules compiled */ 
//}

app.Run();
