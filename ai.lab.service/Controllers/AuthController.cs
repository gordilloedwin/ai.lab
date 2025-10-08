using ai.lab.service.Model;
using ai.lab.service.Model.Database;
using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ai.lab.service.Controllers;

[Route("auth")]
[ApiController]
public class AuthController(ILogger<AuthController> logger, IAuthService authService) : ControllerBase
{
    /// <summary>
    /// Authenticates a user based on the provided sign-in credentials and returns a token if authentication is
    /// successful.
    /// </summary>
    /// <remarks>Returns <see cref="StatusCodes.Status200OK"/> with the token on successful authentication,
    /// <see cref="StatusCodes.Status400BadRequest"/> if required credentials are missing, <see
    /// cref="StatusCodes.Status401Unauthorized"/> if credentials are invalid, and <see
    /// cref="StatusCodes.Status500InternalServerError"/> if an unexpected error occurs.</remarks>
    /// <param name="request">The sign-in request containing the user's email and password. Cannot be null. Both email and password must be
    /// provided and non-empty.</param>
    /// <returns>An <see cref="IActionResult"/> containing the authentication token if sign-in is successful; otherwise, a
    /// response indicating the reason for failure, such as invalid credentials or a server error.</returns>
    [HttpPost("token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(Summary = "Authenticate user and get token",
        Description = "Authenticates a user based on the provided sign-in credentials and returns a token if authentication is successful.")]
    public async Task<IActionResult> GetUserToken([FromBody] SignInRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Email and password must be provided.");
            }

            var user = await authService.SingInUserAsync(request, HttpContext.RequestAborted);
            if (user == null)
            {
                return Unauthorized("Invalid email or password.");
            }

            var token = authService.GenerateToken(user);
            return Ok(token);
        }
        catch (Exception signInException)
        {
            logger.LogError(signInException, "Error occurred during sign-in for email {Email}", request?.Email);
            return StatusCode(500, "An error occurred during sign-in.");
        }
    }

    [Authorize]
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(Summary = "Register a new user",
        Description = "Registers a new user. Only administrators can register new users.")]
    public async Task<IActionResult> Register([FromBody] SignInRequest user)
    {
        try
        {
            var isAdmin = User.Claims.FirstOrDefault(c => c.Type == "isAdmin")?.Value;

            if (isAdmin != "True")
            {
                return Forbid("Only administrators can register new users.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest("Email and password must be provided.");
            }

            var newUser = new User
            {
                Email = user.Email,
                Name = user.Name,
                PasswordHash = user.Password,
                CreatedAt = DateTime.UtcNow,
                IsAdmin = false
            };

            var resultMessage = await authService.AddUserAsync(newUser, HttpContext.RequestAborted);

            if (resultMessage == "User added successfully")
            {
                return Ok(resultMessage);
            }
            else
            {
                return BadRequest(resultMessage);
            }
        }
        catch (Exception registerException)
        {
            logger.LogError(registerException, "Error occurred during registration for email {Email}", user?.Email);
            return StatusCode(500, "An error occurred during registration.");
        }
    }
}
