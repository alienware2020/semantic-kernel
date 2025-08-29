using Microsoft.Extensions.Options;
using Pinecone;
using WebAPI.Interfaces;
using WebAPI.Models;
using UpsertRequest = Pinecone.UpsertRequest;

namespace WebAPI.Services;

public class PineconeVectorStoreService : IVectorStoreService
{
    private readonly PineconeClient _pineconeClient;

    private readonly IndexClient _indexClient;
    private readonly PineconeOptions _options;

    public PineconeVectorStoreService(IOptions<PineconeOptions> options)
    {
        _options = options.Value;
        _pineconeClient = new PineconeClient(_options.ApiKey);
        _indexClient = _pineconeClient.Index(_options.IndexName);
    }

    public async Task<string> UpsertAsync(string id, ReadOnlyMemory<float> embedding,
        Dictionary<string, MetadataValue> metadata, string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var pineconeMetadata = new Metadata();
        foreach (var kvp in metadata)
        {
            pineconeMetadata[kvp.Key] = kvp.Value;
        }

        var vector = new Vector
        {
            Id = id,
            Values = embedding.ToArray(),
            Metadata = pineconeMetadata
        };

        var upsertRequest = new UpsertRequest { Vectors = [vector] };
        if (!string.IsNullOrEmpty(tenantId))
        {
            upsertRequest.Namespace = tenantId;
        }

        await _indexClient.UpsertAsync(upsertRequest, cancellationToken: cancellationToken);
        return id;
    }

    public async Task<List<VectorMatch>> QueryAsync(ReadOnlyMemory<float> queryEmbedding, int topK = 10,
        Dictionary<string, object>? filter = null, string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var queryRequest = new QueryRequest
        {
            Vector = queryEmbedding.ToArray(),
            TopK = (uint)topK,
            IncludeMetadata = true,
            IncludeValues = true
        };

        if (!string.IsNullOrEmpty(tenantId))
        {
            queryRequest.Namespace = tenantId;
        }

        if (filter is { Count: > 0 })
        {
            queryRequest.Filter = new Metadata();
            // TODO: Implement proper filter mapping
        }

        var response = await _indexClient.QueryAsync(queryRequest, cancellationToken: cancellationToken);

        return response.Matches
            .Select(match => new VectorMatch(
                match.Id,
                match.Score,
                match.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString())))
            .ToList();
    }

    public async Task DeleteAsync(IEnumerable<string> ids, string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var deleteRequest = new DeleteRequest { Ids = ids };
        if (!string.IsNullOrEmpty(tenantId))
        {
            deleteRequest.Namespace = tenantId;
        }

        await _indexClient.DeleteAsync(deleteRequest, cancellationToken: cancellationToken);
    }

    public async Task DeleteAllAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        
        var deleteRequest = new DeleteRequest
        {
            Namespace = tenantId,
            DeleteAll = true
        };
        await _indexClient.DeleteAsync(deleteRequest, cancellationToken: cancellationToken);
    }
}