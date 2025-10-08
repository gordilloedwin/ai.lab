using ai.lab.service;
using ai.lab.service.HealthCheck;
using ai.lab.service.Managers;
using ai.lab.service.Metrics;
using ai.lab.service.Options;
using ai.lab.service.Services;
using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var meters = new OtelMetrics("XRS.Reporting.DriverLogService");
var jwtOptions = builder.Configuration.GetSection("JwtOptions").Get<JwtOptions>() ?? new JwtOptions();
var otelOptions = builder.Configuration.GetSection("OtelOptions").Get<OtelOptions>() ?? new OtelOptions();

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
        Version = "2.0",        
        Description = "API for AI Lab Service",
        Contact = new OpenApiContact
        {
            Name = "AI & ML Lab Service",
            Email = "gordilloedwin@hotmail.com"
        }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
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
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
builder.Services.AddSingleton(meters);
builder.Services.TryAddScoped<IAIService, AIService>();
builder.Services.TryAddScoped<IAuthService, AuthService>();
builder.Services.TryAddScoped<IDatabaseService, DatabaseService>();
builder.Services.TryAddScoped<IOllamaClient, OllamaClientManager>();
builder.Services.TryAddScoped<IQdrantClient, QdrantClientManager>();
builder.Services.TryAddScoped<IEmbeddingManager, EmbeddingManager>();
builder.Services.TryAddScoped<IContextSessionManager, ContextSessionManager>();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddHostedService<AiLabWorker>();

builder.Services.AddHealthChecks()
    .AddCheck<CacheHealthCheck>("cache_health_check")
    .AddCheck<OllamaHealthCheck>("ollama_health_check")
    .AddCheck<DatabaseHealthCheck>("database_health_check");

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseSwagger(options => options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0);
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "AI Lab Service API Docs";
    options.RoutePrefix = "swagger";
    var swaggerJsonBasePath = string.IsNullOrEmpty(options.RoutePrefix) ? "." : "..";
    options.SwaggerEndpoint($"{swaggerJsonBasePath}/swagger/v1/swagger.json", "AI Lab Service API v1");
});

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/healthcheck");
app.MapRazorComponents<ai.lab.service.Components.App>().AddInteractiveServerRenderMode();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.MapHub<AiLabHub>("/ailabchat");
app.Run();