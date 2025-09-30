using ai.lab.service;
using ai.lab.service.Components;
using ai.lab.service.Services;

var builder = WebApplication.CreateBuilder(args);

// Use Kestrel as the web server
builder.WebHost.UseKestrel();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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