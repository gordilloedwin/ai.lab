using ai.lab.service.Model.Database;

namespace ai.lab.service.Services.Common;

public interface ITokenService
{
    string GenerateToken(User user);
}
