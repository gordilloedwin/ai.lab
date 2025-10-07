using ai.lab.service;
using ai.lab.service.Components;
using ai.lab.service.HealthCheck;
using ai.lab.service.Managers;
using ai.lab.service.Services;
using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Polly;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AI Lab Service API",
        Version = "2.0",        
        Description = "API for AI Lab Service",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "AI & ML Lab Service",
            Email = "gordilloedwin@hotmail.com"
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
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!))
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
builder.Services.AddScoped<IAIService, AIService>();
builder.Services.AddScoped<IDatabaseService, DatabaseService>();
builder.Services.AddScoped<IOllamaClient, OllamaClientManager>();
builder.Services.AddScoped<IQdrantClient, QdrantClientManager>();
builder.Services.AddScoped<IEmbeddingManager, EmbeddingManager>();
builder.Services.AddScoped<IContextSessionManager, ContextSessionManager>();
builder.Services.AddHostedService<AiLabWorker>();

builder.Services.AddHealthChecks()
    .AddCheck<CacheHealthCheck>("cache_health_check")
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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/healthcheck");
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.MapHub<AiLabHub>("/ailabchat");
app.Run();