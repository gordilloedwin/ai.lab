using ai.lab.service;
using ai.lab.service.Components;
using ai.lab.service.Services;
using ai.lab.service.Services.Common;

var builder = WebApplication.CreateBuilder(args);

// Use Kestrel as the web server
builder.WebHost.UseKestrel();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AI Lab Service API",
        Version = "v1.0.0",
        Description = "API for AI Lab Service",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "AI & ML Lab Service",
            Email = "gordilloedwin@hotmail.com"
        }
    });
});

// Add Blazor Server services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add SignalR services
builder.Services.AddSignalR();

// Add AI Service
builder.Services.AddScoped<IAIService, AIService>();
 
// Add the background worker service
builder.Services.AddHostedService<AiLabWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "AI Lab Service API Docs";
    options.RoutePrefix = "swagger";
    var swaggerJsonBasePath = string.IsNullOrEmpty(options.RoutePrefix) ? "." : "..";
    options.SwaggerEndpoint($"{swaggerJsonBasePath}/swagger/v1/swagger.json", "AI Lab Service API v1");
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthorization();
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<AIService>("/index");
app.Run();