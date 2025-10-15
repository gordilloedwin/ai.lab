using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private readonly ProtectedLocalStorage _localStorage;
    private readonly ILogger<JwtAuthStateProvider> _logger;
    private ClaimsPrincipal _user = new(new ClaimsIdentity());
    private bool _attemptedRestore = false;
    private System.Threading.Timer? _expiryTimer;

    public JwtAuthStateProvider(ProtectedLocalStorage localStorage, ILogger<JwtAuthStateProvider> logger)
    {
        _localStorage = localStorage;
        _logger = logger;
    }

    public void SetToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var claims = jwtToken.Claims;
            var identity = new ClaimsIdentity(claims, "jwt");
            _user = new ClaimsPrincipal(identity);
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
                var stored = await _localStorage.GetAsync<string>("jwt");
                if (stored.Success && !string.IsNullOrWhiteSpace(stored.Value))
                {
                    SetToken(stored.Value!);
                    _logger.LogInformation("Restored JWT from ProtectedLocalStorage");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed restoring JWT from ProtectedLocalStorage");
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