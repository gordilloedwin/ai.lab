using ai.lab.service.Model;
using ai.lab.service.Model.Database;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ai.lab.service.Services;

public class AuthService(ILogger<AuthService> logger, IOptionsMonitor<JwtOptions> jwtOptions, IDatabaseService databaseService) : IAuthService
{
    public async Task<string> AddUserAsync(User user, CancellationToken cancellationToken)
    {
        if (user == null) {
            logger.LogError("Attempted to add a null user");
            return "Attempted to add a null user";
        }

        if (string.IsNullOrWhiteSpace(user.Email)) {
            logger.LogError("Attempted to add a user with empty email");
            return "Attempted to add a user with empty email";
        }

        var existingUser = await databaseService.GetUserByEmailAsync(user.Email, cancellationToken);
        if (existingUser != null) {
            logger.LogWarning("User with email {Email} already exists", user.Email);
            return "User with this email already exists";
        }

        user.PasswordHash = HashPassword(user.PasswordHash ?? string.Empty);
        return await databaseService.AddUserAsync(user, cancellationToken) ? "User added successfully" : "Failed to add user";
    }

    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken) => 
        await databaseService.GetUserByEmailAsync(email, cancellationToken);

    public async Task<User?> SingInUserAsync(SignInRequest signInRequest, CancellationToken cancellationToken)
    {
        try
        {
            var user = await databaseService.GetUserByEmailAsync(signInRequest.Email, cancellationToken);
            if (user == null)
            {
                logger.LogWarning("User with email {Email} not found", signInRequest.Email);
                return null;
            }

            if (string.IsNullOrEmpty(user.PasswordHash) || !VerifyPassword(signInRequest.Password ?? string.Empty, user.PasswordHash))
            {
                logger.LogWarning("Invalid password for user with email {Email}", signInRequest.Email);
                return null;
            }

            return user;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed signing in user with email {Email}", signInRequest.Email);
            return null;
        }
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

    public async Task<string> LoginAsync(SignInRequest signInRequest)
    {
        var user = await SingInUserAsync(signInRequest, CancellationToken.None);
        if (user == null)
        {
            return "Invalid email or password";
        }

        var token = GenerateToken(user);
        if (string.IsNullOrEmpty(token))
        {
            return "Failed to generate authentication token";
        }

        return token;
    }

    /// <summary>
    /// Generates a secure hash for the specified password using PBKDF2 with a random salt.
    /// </summary>
    /// <remarks>The returned string includes both the salt and the hash, allowing for password verification
    /// without storing the salt separately. The method uses 100,000 iterations and SHA-256 for key derivation,
    /// providing strong resistance against brute-force attacks.</remarks>
    /// <param name="password">The password to be hashed. Cannot be null.</param>
    /// <returns>A Base64-encoded string containing the salt and hash. This value can be stored for later password verification.</returns>
    private string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(16);
        var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);
        byte[] result = new byte[48];
        Buffer.BlockCopy(salt, 0, result, 0, 16);
        Buffer.BlockCopy(hash, 0, result, 16, 32);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Verifies whether the specified password matches the stored password hash using PBKDF2 with SHA-256.
    /// </summary>
    /// <remarks>This method uses a fixed-time comparison to help prevent timing attacks. The stored hash must
    /// be in the expected format: a base64-encoded combination of a 16-byte salt followed by a 32-byte hash. Supplying
    /// an incorrectly formatted hash may result in an exception.</remarks>
    /// <param name="password">The plaintext password to verify against the stored hash.</param>
    /// <param name="storedHash">The base64-encoded password hash, including the salt and hash, as previously generated by the password hashing
    /// process.</param>
    /// <returns>true if the password matches the stored hash; otherwise, false.</returns>
    private bool VerifyPassword(string password, string storedHash)
    {
        byte[] storedBytes = Convert.FromBase64String(storedHash);
        byte[] salt = new byte[16];
        Buffer.BlockCopy(storedBytes, 0, salt, 0, 16);
        byte[] storedHashBytes = new byte[32];
        Buffer.BlockCopy(storedBytes, 16, storedHashBytes, 0, 32);
        var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        byte[] computedHash = pbkdf2.GetBytes(32);

        return CryptographicOperations.FixedTimeEquals(computedHash, storedHashBytes);
    }
}
