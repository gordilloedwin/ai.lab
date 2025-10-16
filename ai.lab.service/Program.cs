using ai.lab.service;
using ai.lab.service.HealthCheck;
using ai.lab.service.Managers;
using ai.lab.service.Metrics;
using ai.lab.service.Options;
using ai.lab.service.Services;
using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using System.Net;
using System.Text;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var aiLabOtelMeter = new OtelMetrics("AI.Lab.Service");
var jwtOptions = builder.Configuration.GetSection("JwtOptions").Get<JwtOptions>() ?? new JwtOptions();
var openTelemetryOptions = builder.Configuration.GetSection("OtelOptions").Get<OtelOptions>() ?? new OtelOptions();

builder.WebHost.UseKestrel();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddHttpClient(OllamaClientManager.HttpClientName)
    .AddPolicyHandler(Policy.WrapAsync(OllamaClientManager.GetRetryPolicy(), OllamaClientManager.GetCircuitBreakerPolicy()));
builder.Services.AddHttpClient(QdrantClientManager.HttpClientName)
    .AddPolicyHandler(Policy.WrapAsync(QdrantClientManager.GetRetryPolicy(), QdrantClientManager.GetCircuitBreakerPolicy()));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("JwtOptions"));
builder.Services.Configure<AILabOptions>(builder.Configuration.GetSection("AILabOptions"));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("DatabaseOptions"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AI Lab Service API",
        Version = "v1",        
        Description = "API for AI Lab Service",
        Contact = new OpenApiContact
        {
            Name = "AI & ML Lab Service",
            Email = "gordilloedwin@hotmail.com"
        }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by your JWT token. Example: Bearer abc123"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "Bearer",
                Name = "Authorization",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/ailabchat"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(aiLabOtelMeter);
builder.Services.TryAddScoped<IAIService, AIService>();
builder.Services.TryAddScoped<IAuthService, AuthService>();
builder.Services.TryAddScoped<IDatabaseService, DatabaseService>();
builder.Services.TryAddScoped<IOllamaClient, OllamaClientManager>();
builder.Services.TryAddScoped<IQdrantClient, QdrantClientManager>();
builder.Services.TryAddScoped<IEmbeddingManager, EmbeddingManager>();
builder.Services.TryAddScoped<IContextSessionManager, ContextSessionManager>();
builder.Services.TryAddScoped<AuthenticationStateProvider, JwtAuthStateProvider>();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddAuthorizationCore();
builder.Services.AddHostedService<AiLabWorker>();

builder.Services.AddHealthChecks()
    .AddCheck<CacheHealthCheck>("cache_health_check")
    .AddCheck<OllamaHealthCheck>("ollama_health_check")
    .AddCheck<DatabaseHealthCheck>("database_health_check");

#region Telemetry
if (openTelemetryOptions.Enabled)
{
    builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(aiLabOtelMeter.MeterName));
        metrics.AddMeter(aiLabOtelMeter.MeterName);
        metrics.AddInstrumentation(aiLabOtelMeter.ActivitySource);
        metrics.AddHttpClientInstrumentation();
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddRuntimeInstrumentation();
        metrics.AddView("ws_service_call_duration_ticks", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = OtelMetrics.histogramBuckets,
            Name = "ws_service_call_duration_ticks"
        });
        metrics.AddView("ai_qdrant_database_call_duration_ticks", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = OtelMetrics.histogramBuckets,
            Name = "ai_qdrant_database_call_duration_ticks"
        });

        if (openTelemetryOptions.EnableConsoleExporter)
        {
            metrics.AddConsoleExporter();
        }

        if (!string.IsNullOrEmpty(openTelemetryOptions.ExporterOptions.Endpoint))
        {
            metrics.AddOtlpExporter(telemetryOptions =>
            {
                telemetryOptions.TimeoutMilliseconds = 10000;
                telemetryOptions.HttpClientFactory = () =>
                {
                    var client = new HttpClient();
                    client.Timeout = TimeSpan.FromMilliseconds(10000);
                    return client;
                };
                telemetryOptions.Endpoint = new Uri(openTelemetryOptions.ExporterOptions.Endpoint);
                telemetryOptions.Protocol = openTelemetryOptions.ExporterOptions.Protocol.ToLower() == "grpc" ?
                                            OpenTelemetry.Exporter.OtlpExportProtocol.Grpc :
                                            OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;                
            });
        }
    })
    .WithTracing(tracing =>
    {
        tracing.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(aiLabOtelMeter.ActivitySource.Name));
        tracing.AddHttpClientInstrumentation();
        tracing.AddSource(aiLabOtelMeter.ActivitySource.Name);
        tracing.AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/healthcheck");
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                activity.SetTag("http.request.method", request.Method);
                activity.SetTag("http.request.url", request.Path);
            };
        });

        if (openTelemetryOptions.EnableConsoleExporter)
        {
            tracing.AddConsoleExporter();
        }

        if (!string.IsNullOrEmpty(openTelemetryOptions.ExporterOptions.Endpoint))
        {
            tracing.AddOtlpExporter(telemetryOptions =>
            {
                telemetryOptions.TimeoutMilliseconds = 10000;
                telemetryOptions.HttpClientFactory = () =>
                {
                    var client = new HttpClient();
                    client.Timeout = TimeSpan.FromMilliseconds(10000);
                    return client;
                };
                telemetryOptions.Endpoint = new Uri(openTelemetryOptions.ExporterOptions.Endpoint);
                telemetryOptions.Protocol = openTelemetryOptions.ExporterOptions.Protocol.ToLower() == "grpc" ?
                                            OpenTelemetry.Exporter.OtlpExportProtocol.Grpc :
                                            OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            });
        }
    })
    .WithLogging(logging =>
    {
        logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(aiLabOtelMeter.MeterName));
        logging.AddInstrumentation(aiLabOtelMeter.ActivitySource);

        if (openTelemetryOptions.EnableConsoleExporter)
        {
            logging.AddConsoleExporter();
        }

        if (!string.IsNullOrEmpty(openTelemetryOptions.ExporterOptions.Endpoint))
        {
            logging.AddOtlpExporter(telemetryOptions =>
            {
                telemetryOptions.TimeoutMilliseconds = 10000;
                telemetryOptions.HttpClientFactory = () =>
                {
                    var client = new HttpClient();
                    client.Timeout = TimeSpan.FromMilliseconds(10000);
                    return client;
                };
                telemetryOptions.Endpoint = new Uri(openTelemetryOptions.ExporterOptions.Endpoint);
                telemetryOptions.Protocol = openTelemetryOptions.ExporterOptions.Protocol.ToLower() == "grpc" ?
                                            OpenTelemetry.Exporter.OtlpExportProtocol.Grpc :
                                            OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            });
        }
    });
}
#endregion

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseSwagger(options => options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0);
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "AI Lab Service API Docs";
    options.RoutePrefix = "swagger";
    var swaggerJsonBasePath = string.IsNullOrEmpty(options.RoutePrefix) ? "." : "..";
    options.SwaggerEndpoint($"{swaggerJsonBasePath}/swagger/v1/swagger.json", "AI Lab Service API v1");
});

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseRouting();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();
app.MapHealthChecks("/healthcheck");
app.MapHub<AiLabHub>("/ailabchat");
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

try
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RoutesAudit");
    var endpointDataSources = scope.ServiceProvider.GetServices<EndpointDataSource>();
    var endpoints = endpointDataSources
        .SelectMany(ds => ds.Endpoints)
        .OfType<RouteEndpoint>()
        .Select(e => new
        {
            Route = e.RoutePattern.RawText,
            Order = e.Order,
            Display = e.DisplayName
        })
        .OrderBy(e => e.Route)
        .ThenBy(e => e.Order)
        .ToList();

    if (endpoints.Count > 0)
    {
        logger.LogDebug("Route audit start: {Count} endpoints discovered", endpoints.Count);
        foreach (var ep in endpoints)
        {
            logger.LogDebug("Route: {Route,-25} | Order: {Order,3} | Display: {Display}", ep.Route, ep.Order, ep.Display);
        }
        logger.LogDebug("Route audit end");
    }
    else
    {
        logger.LogWarning("Route audit found zero endpoints.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Route audit failed: {ex.Message}");
}

app.Run();