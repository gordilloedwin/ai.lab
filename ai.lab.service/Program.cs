using ai.lab.service;
using ai.lab.service.Components;
using ai.lab.service.Services;
using Microsoft.OpenApi.Models;

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
        Description = "API for AI Lab Service - A hybrid worker and web API service",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "AI Lab Service",
            Email = "support@ailab.local"
        }
    });
});

// Add Blazor Server services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add SignalR services
builder.Services.AddSignalR();

// Add the background worker service
builder.Services.AddHostedService<AiLabWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthorization();
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<AIService>("/ollamaHub");
app.Run();