using ai.lab.service.Model.Database;

namespace ai.lab.service.Services.Common;

public interface IAuthService
{
    string GenerateToken(User user);

    Task<bool> AddUser(User user);

    Task<User?> GetUserByEmail(string email);
}
