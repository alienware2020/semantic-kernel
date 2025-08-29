using Pinecone;
using WebAPI.Interfaces;
using WebAPI.Models;

namespace WebAPI.Services;

public class RAGService : IRAGService
{
    private readonly ISemanticKernelService _semanticKernelService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IDocumentChunkingService _chunkingService;
    private readonly ILogger<RAGService> _logger;

    public RAGService(
        ISemanticKernelService semanticKernelService,
        IVectorStoreService vectorStoreService,
        IDocumentChunkingService chunkingService,
        ILogger<RAGService> logger)
    {
        _semanticKernelService = semanticKernelService;
        _vectorStoreService = vectorStoreService;
        _chunkingService = chunkingService;
        _logger = logger;
    }

    public async Task<string> StoreDocumentAsync(string content, Dictionary<string, string> metadata,
        string? tenantId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var documentId = Guid.NewGuid().ToString();

            // Generate embedding for the content
            var embedding = await _semanticKernelService.GenerateEmbeddingAsync(content, cancellationToken);

            // TODO: Add namespace for multi-tenancy support
            // Prepare metadata with content and timestamp
            var enrichedMetadata = new Dictionary<string, MetadataValue>(metadata.ToDictionary(
                kvp => kvp.Key,
                kvp => new MetadataValue(kvp.Value)))
            {
                ["content"] = new(content),
                ["timestamp"] = new(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                ["document_id"] = new(documentId)
            };

            // Store in vector database
            await _vectorStoreService.UpsertAsync(documentId, embedding, enrichedMetadata, tenantId, cancellationToken);

            _logger.LogInformation("Document stored successfully with ID: {DocumentId}", documentId);
            return documentId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing document");
            throw;
        }
    }

    public async Task<DocumentProcessingResult> ProcessAndStoreDocumentAsync(string content, string fileName,
        Dictionary<string, string>? metadata = null, string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var documentId = Guid.NewGuid().ToString();
            metadata ??= new Dictionary<string, string>();

            // Add document-level metadata
            metadata["document_id"] = documentId;
            metadata["document_type"] = "processed_document";

            _logger.LogInformation("Processing document {FileName} with ID {DocumentId}", fileName, documentId);

            // Chunk the document
            var chunks = await _chunkingService.ProcessDocumentAsync(content, fileName, documentId, metadata);

            var chunkIds = new List<string>();
            var tasks = new List<Task>();

            // Process chunks in parallel
            foreach (var chunk in chunks)
            {
                var task = ProcessAndStoreChunkAsync(chunk, tenantId, cancellationToken);
                tasks.Add(task);
                chunkIds.Add(chunk.ChunkId);
            }

            await Task.WhenAll(tasks);

            _logger.LogInformation("Successfully processed and stored {ChunkCount} chunks for document {DocumentId}",
                chunks.Count, documentId);

            return new DocumentProcessingResult(
                documentId,
                fileName,
                chunks.Count,
                chunkIds,
                metadata
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document {FileName}", fileName);
            throw;
        }
    }

    public async Task<DocumentProcessingResult> ProcessAndStoreDocumentFromStreamAsync(Stream fileStream,
        string fileName,
        Dictionary<string, string>? metadata = null, string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract text content from file stream
            var content = await _chunkingService.ExtractTextFromFileAsync(fileStream, fileName);

            // Process the extracted content
            return await ProcessAndStoreDocumentAsync(content, fileName, metadata, tenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document from stream {FileName}", fileName);
            throw;
        }
    }

    private async Task ProcessAndStoreChunkAsync(DocumentChunk chunk, string? tenantId,
        CancellationToken cancellationToken)
    {
        // Generate embedding for the chunk
        var embedding = await _semanticKernelService.GenerateEmbeddingAsync(chunk.Content, cancellationToken);

        // Convert chunk metadata to MetadataValue dictionary
        var enrichedMetadata = chunk.Metadata.ToDictionary(
            kvp => kvp.Key,
            kvp => new MetadataValue(kvp.Value));

        // Add additional metadata
        enrichedMetadata["content"] = new MetadataValue(chunk.Content);
        enrichedMetadata["timestamp"] = new MetadataValue(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        enrichedMetadata["chunk_start_position"] = new MetadataValue(chunk.StartPosition);
        enrichedMetadata["chunk_end_position"] = new MetadataValue(chunk.EndPosition);

        // Store in vector database
        await _vectorStoreService.UpsertAsync(chunk.ChunkId, embedding, enrichedMetadata, tenantId, cancellationToken);
    }

    public async Task<List<RAGResult>> SearchSimilarAsync(string query, int topK = 5,
        Dictionary<string, object>? filter = null, string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate embedding for the query
            var queryEmbedding = await _semanticKernelService.GenerateEmbeddingAsync(query, cancellationToken);

            // Search for similar vectors
            var matches =
                await _vectorStoreService.QueryAsync(queryEmbedding, topK, filter, tenantId, cancellationToken);

            // Convert to RAG results
            var results = matches.Select(match => new RAGResult(
                match.Id,
                match.Score,
                match.Metadata.GetValueOrDefault("content", "")?.ToString() ?? "",
                match.Metadata.Where(kvp => kvp.Key != "content")
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            )).ToList();

            _logger.LogInformation("Found {Count} similar documents for query", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for similar documents");
            throw;
        }
    }

    public async Task<string> GenerateResponseAsync(string query, int topK = 5,
        string? tenantId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find relevant documents
            var similarDocs = await SearchSimilarAsync(query, topK, filter: null, tenantId, cancellationToken);

            if (!similarDocs.Any())
            {
                return "I don't have enough information to answer your question.";
            }

            // Build context from retrieved documents
            var context = string.Join("\n\n", similarDocs.Select((doc, index) =>
                $"Document {index + 1}:\n{doc.Content}"));

            Console.WriteLine("Context:\n" + context);

            // Create RAG prompt
            var prompt = $"""
                          Based on the following context, please answer the user's question. If the answer is not in the context, say so.

                          Context:
                          {context}

                          Question: {query}

                          Answer:
                          """;

            // Generate response using the LLM
            var response = await _semanticKernelService.GenerateResponseAsync(prompt, cancellationToken);

            _logger.LogInformation("Generated RAG response for query: {Query}", query);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating RAG response");
            throw;
        }
    }

    public async Task DeleteDocumentAsync(string documentId, string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _vectorStoreService.DeleteAsync([documentId], tenantId, cancellationToken);
            _logger.LogInformation("Document deleted successfully: {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document: {DocumentId}", documentId);
            throw;
        }
    }

    public async Task DeleteAllDocumentsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

            await _vectorStoreService.DeleteAllAsync(tenantId, cancellationToken);
            _logger.LogInformation("All documents deleted successfully for tenant: {TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all documents for tenant: {TenantId}", tenantId);
            throw;
        }
    }
}