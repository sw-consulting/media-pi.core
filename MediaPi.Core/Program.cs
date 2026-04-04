// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Middleware;
using MediaPi.Core.Services;
using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;
config
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    // Optionally load the override config if present
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

// Read max upload size from configuration (bytes). Default to 4 GiB if not set.
var maxUploadBytes = config.GetValue<long?>("VideoStorage:MaxUploadSizeBytes") ?? 4L * 1024 * 1024 * 1024;

var certPath = config["Kestrel:Certificates:Default:Path"];
var certPassword = config["Kestrel:Certificates:Default:Password"];

bool useHttps = !string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(certPassword) && File.Exists(certPath);
builder.WebHost.ConfigureKestrel(options =>
{
     // Apply max request body size to Kestrel
     options.Limits.MaxRequestBodySize = maxUploadBytes;

     if (useHttps)
     { 
            options.ListenAnyIP(8081, listenOptions => listenOptions.UseHttps(certPath!, certPassword));
     }
     options.ListenAnyIP(8080);
});


builder.Services
    .Configure<AppSettings>(config.GetSection("AppSettings"))
    .Configure<VideoStorageSettings>(config.GetSection("VideoStorage"))
    .Configure<DeviceMonitorSettings>(config.GetSection("DeviceMonitoringSettings"))
    .AddScoped<IJwtUtils, JwtUtils>()
    .AddScoped<IUserInformationService, UserInformationService>()
    .AddHttpContextAccessor()
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// Configure multipart/form-data limits to match Kestrel setting
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadBytes; // bytes
});

builder.Services.AddHttpClient<IMediaPiAgentClient, DeviceAgentRestClient>(client =>
{
    // timeout for all agent requests
    client.Timeout = TimeSpan.FromSeconds(15);
})
;

builder.Services.AddScoped<LoggingHandler>();

builder.Services.AddHttpClient<IMediaPiAgentClient2, MediaPiAgentClient2>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
})
.AddHttpMessageHandler<LoggingHandler>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

// Register DeviceEventsService as singleton
builder.Services.AddSingleton<DeviceEventsService>();

builder.Services.AddSingleton<DeviceMonitoringService>();
builder.Services.AddSingleton<IDeviceMonitoringService>(sp => sp.GetRequiredService<DeviceMonitoringService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DeviceMonitoringService>());
builder.Services.AddSingleton<IVideoMetadataService, VideoMetadataService>();
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();
builder.Services.AddSingleton<IVideoStorageService, VideoStorageService>();
builder.Services.AddSingleton<IScreenshotStorageService, ScreenshotStorageService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Media Pi Core Api", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization token. Example: \"Authorization: {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });

    var scm = new OpenApiSecurityScheme
    {
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { scm, Array.Empty<string>() } });
});

var app = builder.Build();

// Apply pending migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app
    .UseMiddleware<JwtMiddleware>()
    .UseMiddleware<AuthorizeDeviceByXIdMiddleware>()
    .UseMiddleware<DatabaseConstraintMiddleware>()
    .UseSwagger()
    .UseSwaggerUI();
if (useHttps)
{
    app.UseHttpsRedirection();
}

app
    .UseCors()
    .UseAuthorization();

app.MapControllers();
app.Run();
