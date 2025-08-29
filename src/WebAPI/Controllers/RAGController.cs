using Microsoft.AspNetCore.Mvc;
using WebAPI.Interfaces;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RAGController : ControllerBase
{
    private readonly ISemanticKernelService _semanticKernelService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IRAGService _ragService;
    private readonly ILogger<RAGController> _logger;

    public RAGController(
        ISemanticKernelService semanticKernelService,
        IVectorStoreService vectorStoreService,
        IRAGService ragService,
        ILogger<RAGController> logger)
    {
        _semanticKernelService = semanticKernelService;
        _vectorStoreService = vectorStoreService;
        _ragService = ragService;
        _logger = logger;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }

    [HttpPost("test-embedding")]
    public async Task<IActionResult> TestEmbedding([FromBody] TestEmbeddingRequest request)
    {
        try
        {
            var embedding = await _semanticKernelService.GenerateEmbeddingAsync(request.Text);
            return Ok(new
            {
                Text = request.Text,
                EmbeddingDimensions = embedding.Length,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding for text: {Text}", request.Text);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("test-generation")]
    public async Task<IActionResult> TestGeneration([FromBody] TestGenerationRequest request)
    {
        try
        {
            var response = await _semanticKernelService.GenerateResponseAsync(request.Prompt);
            return Ok(new
            {
                Prompt = request.Prompt,
                Response = response,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response for prompt: {Prompt}", request.Prompt);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("store-document")]
    public async Task<IActionResult> StoreDocument([FromBody] StoreDocumentRequest request, [FromHeader(Name = "X-Tenant-Id")] string? tenantId = null)
    {
        try
        {
            var documentId = await _ragService.StoreDocumentAsync(request.Content,
                request.Metadata ?? new Dictionary<string, string>(), tenantId);
            return Ok(new
            {
                DocumentId = documentId,
                Content = request.Content,
                TenantId = tenantId,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing document");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("upload-document")]
    public async Task<IActionResult> UploadDocument(
        IFormFile file, 
        [FromHeader(Name = "X-Tenant-Id")] string? tenantId = null,
        [FromForm] string? metadata = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { Error = "No file uploaded" });
            }

            var metadataDict = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(metadata))
            {
                // Parse JSON metadata if provided
                try
                {
                    var jsonMetadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(metadata);
                    if (jsonMetadata != null)
                    {
                        metadataDict = jsonMetadata;
                    }
                }
                catch
                {
                    // If JSON parsing fails, treat as simple key-value
                    metadataDict["description"] = metadata;
                }
            }

            using var stream = file.OpenReadStream();
            var result = await _ragService.ProcessAndStoreDocumentFromStreamAsync(stream, file.FileName, metadataDict, tenantId);
            
            return Ok(new
            {
                DocumentId = result.DocumentId,
                FileName = result.FileName,
                TotalChunks = result.TotalChunks,
                ChunkIds = result.ChunkIds,
                TenantId = tenantId,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading and processing document: {FileName}", file?.FileName ?? "unknown");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("process-document")]
    public async Task<IActionResult> ProcessDocument([FromBody] ProcessDocumentRequest request, [FromHeader(Name = "X-Tenant-Id")] string? tenantId = null)
    {
        try
        {
            var result = await _ragService.ProcessAndStoreDocumentAsync(
                request.Content, 
                request.FileName, 
                request.Metadata, 
                tenantId);
            
            return Ok(new
            {
                DocumentId = result.DocumentId,
                FileName = result.FileName,
                TotalChunks = result.TotalChunks,
                ChunkIds = result.ChunkIds,
                TenantId = tenantId,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document: {FileName}", request.FileName);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request, [FromHeader(Name = "X-Tenant-Id")] string? tenantId = null)
    {
        try
        {
            var results = await _ragService.SearchSimilarAsync(request.Query, request.TopK ?? 5, request.Filter, tenantId);
            return Ok(new
            {
                Query = request.Query,
                Results = results,
                TenantId = tenantId,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents for query: {Query}", request.Query);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskRequest request, [FromHeader(Name = "X-Tenant-Id")] string? tenantId = null)
    {
        try
        {
            var response = await _ragService.GenerateResponseAsync(request.Question, request.TopK ?? 5, tenantId);
            return Ok(new
            {
                Question = request.Question,
                Answer = response,
                TenantId = tenantId,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating RAG response for question: {Question}", request.Question);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpDelete("document/{documentId}")]
    public async Task<IActionResult> DeleteDocument(string documentId, [FromHeader(Name = "X-Tenant-Id")] string? tenantId = null)
    {
        try
        {
            await _ragService.DeleteDocumentAsync(documentId, tenantId);
            return Ok(new
            {
                DocumentId = documentId,
                TenantId = tenantId,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document: {DocumentId}", documentId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpDelete("tenant/documents")]
    public async Task<IActionResult> DeleteAllDocuments([FromHeader(Name = "X-Tenant-Id")] string tenantId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return BadRequest(new { Error = "X-Tenant-Id header is required for this operation" });
            }

            await _ragService.DeleteAllDocumentsAsync(tenantId);
            return Ok(new
            {
                Message = $"All documents deleted successfully for tenant: {tenantId}",
                TenantId = tenantId,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all documents for tenant: {TenantId}", tenantId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

public record TestEmbeddingRequest(string Text);

public record TestGenerationRequest(string Prompt);

public record StoreDocumentRequest(string Content, Dictionary<string, string>? Metadata);

public record ProcessDocumentRequest(string Content, string FileName, Dictionary<string, string>? Metadata);

public record SearchRequest(string Query, int? TopK, Dictionary<string, object>? Filter);

public record AskRequest(string Question, int? TopK);