using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace ai.lab.service.Services.Auth;

public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<JwtAuthStateProvider> _logger;
    private ClaimsPrincipal _user = new(new ClaimsIdentity());
    private bool _attemptedRestore = false;
    private System.Threading.Timer? _expiryTimer;

    public JwtAuthStateProvider(IJSRuntime jsRuntime, ILogger<JwtAuthStateProvider> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task MarkUserAsAuthenticatedAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        
        try
        {
            // Store token in localStorage
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);
            SetToken(token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed storing token in localStorage");
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            // Clear token from localStorage
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
            PerformLogout();
            _logger.LogInformation("User logged out");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed clearing token from localStorage during logout");
        }
    }

    private void SetToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var claims = jwtToken.Claims.ToList();
            
            // Create authenticated identity with JWT bearer scheme
            var identity = new ClaimsIdentity(claims, "Bearer", ClaimTypes.Email, ClaimTypes.Role);
            _user = new ClaimsPrincipal(identity);
            
            _logger.LogInformation("User authenticated: {Email}, IsAuthenticated: {IsAuth}", 
                _user.Identity?.Name, 
                _user.Identity?.IsAuthenticated);
            
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_user)));

            // Schedule auto-logout based on exp claim if present
            var expUnix = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value;
            if (long.TryParse(expUnix, out var seconds))
            {
                var expUtc = DateTimeOffset.FromUnixTimeSeconds(seconds);
                var delay = expUtc - DateTimeOffset.UtcNow;
                if (delay < TimeSpan.Zero)
                {
                    PerformLogout();
                }
                else
                {
                    _expiryTimer?.Dispose();
                    _expiryTimer = new System.Threading.Timer(_ => PerformLogout(), null, delay, Timeout.InfiniteTimeSpan);
                    _logger.LogInformation("Scheduled auto logout in {Minutes} minutes", delay.TotalMinutes.ToString("F2"));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed parsing JWT during SetToken");
        }
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_user.Identity!.IsAuthenticated && !_attemptedRestore)
        {
            _attemptedRestore = true;
            try
            {
                var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
                if (!string.IsNullOrWhiteSpace(token))
                {
                    SetToken(token);
                    _logger.LogInformation("Restored JWT from localStorage");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed restoring JWT from localStorage");
            }
        }
        return new AuthenticationState(_user);
    }

    private void PerformLogout()
    {
        _logger.LogInformation("JWT expired; performing auto logout");
        _user = new ClaimsPrincipal(new ClaimsIdentity());
        _expiryTimer?.Dispose();
        _expiryTimer = null;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_user)));
    }
}