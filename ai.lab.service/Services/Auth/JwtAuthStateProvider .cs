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
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);
            SetToken(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed storing token in localStorage");
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

    /// <summary>
    /// Attempts to return the currently cached JWT token if the user is authenticated.
    /// </summary>
    public string? TryGetCachedToken()
    {
        if (_user.Identity?.IsAuthenticated == true)
        {
            // Original token was stored only in localStorage; we can reconstruct by encoding claims again if needed.
            // For simplicity we re-fetch from localStorage via JS if not already restored.
            // Prefer GetCurrentTokenAsync for async retrieval.
            return _cachedToken;
        }
        return null;
    }

    private string? _cachedToken;

    private void SetToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var claims = jwtToken.Claims.ToList();
            
            // Create authenticated identity with Bearer authentication type
            // Use "name" claim as the name identifier (matches what's in the JWT)
            var identity = new ClaimsIdentity(claims, "Bearer", "name", ClaimTypes.Role);
            _user = new ClaimsPrincipal(identity);
            
            _cachedToken = token;
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
                // Check if JavaScript interop is available (not during pre-rendering)
                if (_jsRuntime is IJSInProcessRuntime)
                {
                    // Synchronous JS runtime available, try to get token
                    var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        SetToken(token);
                    }
                }
                else
                {
                    // During pre-rendering or async JS runtime, delay token restoration
                    // This will be called again after the circuit is established
                    _logger.LogDebug("JS runtime not available yet (pre-rendering), deferring authentication");
                }
            }
            catch (InvalidOperationException ex)
            {
                // Expected during pre-rendering when JS interop isn't available
                _logger.LogDebug(ex, "JS interop not available during pre-rendering");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed restoring JWT from localStorage");
            }
        }
        
        return new AuthenticationState(_user);
    }

    /// <summary>
    /// Retrieves the current JWT token. If not already cached, attempts localStorage retrieval.
    /// </summary>
    public async Task<string?> GetCurrentTokenAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cachedToken)) return _cachedToken;
        try
        {
            var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
            if (!string.IsNullOrWhiteSpace(token))
            {
                if (_user.Identity?.IsAuthenticated != true)
                {
                    SetToken(token); // Ensure claims are populated
                }
                return token;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed retrieving token via GetCurrentTokenAsync");
        }
        return null;
    }

    private void PerformLogout()
    {
        _user = new ClaimsPrincipal(new ClaimsIdentity());
        _expiryTimer?.Dispose();
        _expiryTimer = null;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_user)));
    }
}