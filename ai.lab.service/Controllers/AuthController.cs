using ai.lab.service.Model;
using ai.lab.service.Model.Database;
using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.Mvc;

namespace ai.lab.service.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(ILogger<AuthController> logger, IAuthService authService) : ControllerBase
{
    [HttpPost("signin")]
    public async Task<IActionResult> SignIn(SignInRequest request)
    {
        var user = await _userRepo.FindByEmailAsync(request.Email);

        if (user == null)
        {
            user = await _userRepo.CreateAsync(new User
            {
                Email = request.Email,
                Name = request.Name,
                PasswordHash = request.Password != null ? Hash(request.Password) : null,
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized("Invalid password");
        }

        var token = _tokenService.GenerateToken(user);

        return Ok(new { token, user });
    }
}
