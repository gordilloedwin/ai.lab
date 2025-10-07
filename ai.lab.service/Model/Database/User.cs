namespace ai.lab.service.Model.Database;

public class User
{
    public long Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? PasswordHash { get; set; }

    public string? AvatarUri { get; set; }

    public bool IsAdmin { get; set; }

    public DateTime? LastSeen { get; set; }

    public DateTime CreatedAt { get; set; }
}
