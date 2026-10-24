using System.ComponentModel.DataAnnotations;

namespace LoanDocAI.Api;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    [Required]
    [RegularExpression("^(AzureOpenAI|OpenAI)$", ErrorMessage = "Provider must be 'AzureOpenAI' or 'OpenAI'.")]
    public string Provider { get; set; } = "";

    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ChatModel { get; set; } = "";
    public string EmbeddingModel { get; set; } = "";
}
