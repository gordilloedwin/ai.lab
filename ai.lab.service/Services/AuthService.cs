using ai.lab.service.Model.Database;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ai.lab.service.Services;

public class AuthService(ILogger<AuthService> logger, IOptionsMonitor<JwtOptions> jwtOptions, IDatabaseService databaseService) : IAuthService
{
    public Task<bool> AddUser(User user)
    {
        throw new NotImplementedException();
    }

    public string GenerateToken(User user)
    {
        try
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("name", user.Name ?? string.Empty),
                new Claim("is_admin", user.IsAdmin.ToString().ToLowerInvariant()),
                new Claim("avatar_uri", user.AvatarUri ?? string.Empty)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.CurrentValue.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken
            (
                claims: claims,
                signingCredentials: creds,
                issuer: jwtOptions.CurrentValue.Issuer,
                audience: jwtOptions.CurrentValue.Audience,                
                expires: DateTime.UtcNow.AddMinutes(jwtOptions.CurrentValue.ExpireMinutes)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed generating JWT for user {UserId}", user.Id);
            return string.Empty;
        }
    }

    public Task<User?> GetUserByEmail(string email)
    {
        throw new NotImplementedException();
    }
}
