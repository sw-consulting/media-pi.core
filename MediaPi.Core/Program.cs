// Copyright (c) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// This file is a part of Media Pi backend application

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Services;
using MediaPi.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add user-configurable settings file
builder.Configuration
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

var config = builder.Configuration;
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    // Optionally load the override config if present
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);


var certPath = config["Kestrel:Certificates:Default:Path"];
var certPassword = config["Kestrel:Certificates:Default:Password"];

bool useHttps = !string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(certPassword) && File.Exists(certPath);
builder.WebHost.ConfigureKestrel(options =>
{
     if (useHttps)
     { 
            options.ListenAnyIP(8081, listenOptions => listenOptions.UseHttps(certPath!, certPassword));
     }
     options.ListenAnyIP(8080);
});


builder.Services
    .Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"))
    .Configure<DeviceMonitorSettings>(builder.Configuration.GetSection("DeviceMonitor"))
    .AddScoped<IJwtUtils, JwtUtils>()
    .AddScoped<IUserInformationService, UserInformationService>()
    .AddHttpContextAccessor()
    .AddControllers();

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
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register DeviceEventsService as singleton
builder.Services.AddSingleton<DeviceEventsService>();

builder.Services.AddSingleton<DeviceMonitoringService>();
builder.Services.AddSingleton<IDeviceMonitoringService>(sp => sp.GetRequiredService<DeviceMonitoringService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DeviceMonitoringService>());
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
