namespace WebAPI.Models;

public record DocumentChunk(
    string ChunkId,
    string Content,
    int ChunkIndex,
    int StartPosition,
    int EndPosition,
    string OriginalDocumentId,
    Dictionary<string, string> Metadata);

public record DocumentProcessingResult(
    string DocumentId,
    string FileName,
    int TotalChunks,
    List<string> ChunkIds,
    Dictionary<string, string> Metadata);