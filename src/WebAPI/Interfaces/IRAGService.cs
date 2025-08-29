using WebAPI.Models;

namespace WebAPI.Interfaces;

public interface IRAGService
{
    Task<string> StoreDocumentAsync(string content, Dictionary<string, string> metadata, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<DocumentProcessingResult> ProcessAndStoreDocumentAsync(string content, string fileName, Dictionary<string, string>? metadata = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<DocumentProcessingResult> ProcessAndStoreDocumentFromStreamAsync(Stream fileStream, string fileName, Dictionary<string, string>? metadata = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<List<RAGResult>> SearchSimilarAsync(string query, int topK = 5, Dictionary<string, object>? filter = null, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<string> GenerateResponseAsync(string query, int topK = 5, string? tenantId = null, CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(string documentId, string? tenantId = null, CancellationToken cancellationToken = default);
    Task DeleteAllDocumentsAsync(string tenantId, CancellationToken cancellationToken = default);
}

public record RAGResult(string Id, float? Score, string Content, Dictionary<string, string> Metadata);