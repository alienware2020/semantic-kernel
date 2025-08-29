using WebAPI.Models;

namespace WebAPI.Interfaces;

public interface IDocumentChunkingService
{
    Task<List<DocumentChunk>> ChunkTextAsync(string text, string documentId, Dictionary<string, string>? metadata = null);
    Task<List<DocumentChunk>> ProcessDocumentAsync(string content, string fileName, string? documentId = null, Dictionary<string, string>? metadata = null);
    Task<string> ExtractTextFromFileAsync(Stream fileStream, string fileName);
}

public class ChunkingOptions
{
    public int MaxChunkSize { get; set; } = 1000;
    public int OverlapSize { get; set; } = 200;
    public string ChunkingStrategy { get; set; } = "recursive"; // recursive, sentence, paragraph
}