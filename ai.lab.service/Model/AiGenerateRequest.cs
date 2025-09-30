using System.ComponentModel.DataAnnotations;
using ai.lab.service.Enum;

namespace ai.lab.service.Controllers;

/// <summary>
/// Request model for AI generation
/// </summary>
public class AiGenerateRequest
{
    /// <summary>
    /// The Ollama model to use for generation
    /// </summary>
    [Required(ErrorMessage = "Model is required")]
    public OllamaModel Model { get; set; }

    /// <summary>
    /// The prompt text to be processed
    /// </summary>
    [Required(ErrorMessage = "Prompt is required")]
    [StringLength(10000, ErrorMessage = "Prompt cannot exceed 10000 characters")]
    public string Prompt { get; set; } = string.Empty;
}