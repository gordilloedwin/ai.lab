using ai.lab.service.Model;
using ai.lab.service.Model.Database;

namespace ai.lab.service.Services.Common;

public interface IAuthService
{
    /// <summary>
    /// Generates a secure authentication token for the specified user.
    /// </summary>
    /// <param name="user">The user for whom the authentication token is to be generated. Cannot be null.</param>
    /// <returns>A string containing the generated authentication token for the user.</returns>
    string GenerateToken(User user);    

    /// <summary>
    /// Asynchronously adds a new user to the system and returns the unique identifier of the created user.
    /// </summary>
    /// <param name="user">The user information to add. Cannot be null. All required user fields must be populated.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the add operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier of the newly
    /// created user as a string.</returns>
    Task<string> AddUserAsync(User user, CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously retrieves a user by their email address.
    /// </summary>
    /// <param name="email">The email address of the user to retrieve. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the user associated with the
    /// specified email address, or null if no user is found.</returns>
    Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to sign in a user asynchronously using the specified sign-in request.
    /// </summary>
    /// <param name="signInRequest">An object containing the user's credentials and any additional sign-in parameters. Cannot be null.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the sign-in operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the signed-in user if authentication
    /// succeeds; otherwise, null.</returns>
    Task<User?> SingInUserAsync(SignInRequest signInRequest, CancellationToken cancellationToken);
}
