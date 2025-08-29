using Pinecone;

namespace WebAPI.Interfaces;

public interface IVectorStoreService
{
    Task<string> UpsertAsync(string id, ReadOnlyMemory<float> embedding,
        Dictionary<string, MetadataValue> metadata, string? tenantId = null, CancellationToken cancellationToken = default);

    Task<List<VectorMatch>> QueryAsync(ReadOnlyMemory<float> queryEmbedding, int topK = 10,
        Dictionary<string, object>? filter = null, string? tenantId = null, CancellationToken cancellationToken = default);

    Task DeleteAsync(IEnumerable<string> ids, string? tenantId = null, CancellationToken cancellationToken = default);
    
    Task DeleteAllAsync(string tenantId, CancellationToken cancellationToken = default);
}

public record VectorMatch(string Id, float? Score, Dictionary<string, string> Metadata);