namespace WebAPI.Models;

public class PineconeOptions
{
    public const string SectionName = "Pinecone";
    
    public string ApiKey { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public int Dimension { get; set; } = 1536;
}