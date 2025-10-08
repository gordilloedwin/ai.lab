using System.ComponentModel.DataAnnotations;

namespace ai.lab.service.Model;

public record SignInRequest : IValidatableObject
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    [MinLength(4)]
    public required string Password { get; set; }

    [StringLength(100)]
    public string? Name { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            yield return new ValidationResult("Email is required.", new[] { nameof(Email) });
        }
        else if (!new EmailAddressAttribute().IsValid(Email))
        {
            yield return new ValidationResult("Invalid email format.", new[] { nameof(Email) });
        }
        else if (string.IsNullOrEmpty(Name))
        {
            Name = Email.GetHashCode().ToString();
        }
    }
}
