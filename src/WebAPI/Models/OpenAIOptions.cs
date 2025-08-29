namespace WebAPI.Models;

public class OpenAIOptions
{
    public const string SectionName = "OpenAI";
    
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "gpt-4";
    public string EmbeddingModelId { get; set; } = "text-embedding-3-large";
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.7;
}