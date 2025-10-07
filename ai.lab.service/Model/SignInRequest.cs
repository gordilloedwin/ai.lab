namespace ai.lab.service.Model;

public class SignInRequest
{
    public string Email { get; set; } = string.Empty;

    public string? Password { get; set; }

    public string? Name { get; set; } 
}
