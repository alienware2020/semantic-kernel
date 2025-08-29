using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WebAPI.Interfaces;
using WebAPI.Models;

namespace WebAPI.Services;

public class DocumentChunkingService : IDocumentChunkingService
{
    private readonly ChunkingOptions _options;
    private readonly ILogger<DocumentChunkingService> _logger;

    public DocumentChunkingService(IOptions<ChunkingOptions> options, ILogger<DocumentChunkingService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<DocumentChunk>> ChunkTextAsync(string text, string documentId, Dictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        List<DocumentChunk> chunks;
        metadata ??= new Dictionary<string, string>();

        switch (_options.ChunkingStrategy.ToLower())
        {
            case "sentence":
                chunks = ChunkBySentence(text, documentId, metadata);
                break;
            case "paragraph":
                chunks = ChunkByParagraph(text, documentId, metadata);
                break;
            default:
                chunks = ChunkRecursively(text, documentId, metadata);
                break;
        }

        _logger.LogInformation("Created {ChunkCount} chunks for document {DocumentId}", chunks.Count, documentId);
        return chunks;
    }

    public async Task<List<DocumentChunk>> ProcessDocumentAsync(string content, string fileName, string? documentId = null, Dictionary<string, string>? metadata = null)
    {
        documentId ??= Guid.NewGuid().ToString();
        metadata ??= new Dictionary<string, string>();
        
        // Add file metadata
        metadata["filename"] = fileName;
        metadata["processed_at"] = DateTimeOffset.UtcNow.ToString("O");
        metadata["content_length"] = content.Length.ToString();

        return await ChunkTextAsync(content, documentId, metadata);
    }

    public async Task<string> ExtractTextFromFileAsync(Stream fileStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLower();
        
        switch (extension)
        {
            case ".txt":
            case ".md":
                using (var reader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync();
                }
            
            case ".json":
                using (var reader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    var jsonContent = await reader.ReadToEndAsync();
                    // For JSON files, you might want to extract specific fields
                    // For now, just return the raw content
                    return jsonContent;
                }
            
            default:
                throw new NotSupportedException($"File type {extension} is not supported. Supported types: .txt, .md, .json");
        }
    }

    private List<DocumentChunk> ChunkRecursively(string text, string documentId, Dictionary<string, string> metadata)
    {
        var chunks = new List<DocumentChunk>();
        var separators = new[] { "\n\n", "\n", ". ", " " };
        
        var textChunks = SplitTextRecursively(text, separators, 0);
        
        for (var i = 0; i < textChunks.Count; i++)
        {
            var chunk = textChunks[i];
            var chunkId = $"{documentId}-chunk-{i:D4}";
            var startPos = text.IndexOf(chunk.Content, StringComparison.Ordinal);
            
            var chunkMetadata = new Dictionary<string, string>(metadata)
            {
                ["chunk_index"] = i.ToString(),
                ["chunk_id"] = chunkId,
                ["chunking_strategy"] = "recursive"
            };

            chunks.Add(new DocumentChunk(
                chunkId,
                chunk.Content.Trim(),
                i,
                startPos,
                startPos + chunk.Content.Length,
                documentId,
                chunkMetadata));
        }

        return chunks;
    }

    private List<DocumentChunk> ChunkBySentence(string text, string documentId, Dictionary<string, string> metadata)
    {
        var chunks = new List<DocumentChunk>();
        var sentences = SplitIntoSentences(text);
        var currentChunk = new StringBuilder();
        var chunkIndex = 0;
        var startPos = 0;

        for (var i = 0; i < sentences.Count; i++)
        {
            var sentence = sentences[i];
            
            if (currentChunk.Length + sentence.Length > _options.MaxChunkSize && currentChunk.Length > 0)
            {
                // Create chunk
                var chunkContent = currentChunk.ToString().Trim();
                var chunkId = $"{documentId}-chunk-{chunkIndex:D4}";
                
                var chunkMetadata = new Dictionary<string, string>(metadata)
                {
                    ["chunk_index"] = chunkIndex.ToString(),
                    ["chunk_id"] = chunkId,
                    ["chunking_strategy"] = "sentence"
                };

                chunks.Add(new DocumentChunk(
                    chunkId,
                    chunkContent,
                    chunkIndex,
                    startPos,
                    startPos + chunkContent.Length,
                    documentId,
                    chunkMetadata));

                // Start new chunk with overlap
                currentChunk.Clear();
                chunkIndex++;
                startPos = text.IndexOf(chunkContent, StringComparison.Ordinal) + chunkContent.Length;
                
                // Add overlap sentences
                var overlapStart = Math.Max(0, i - 2);
                for (var j = overlapStart; j < i; j++)
                {
                    currentChunk.Append(sentences[j]).Append(' ');
                }
            }
            
            currentChunk.Append(sentence).Append(' ');
        }

        // Add remaining content
        if (currentChunk.Length > 0)
        {
            var chunkContent = currentChunk.ToString().Trim();
            var chunkId = $"{documentId}-chunk-{chunkIndex:D4}";
            
            var chunkMetadata = new Dictionary<string, string>(metadata)
            {
                ["chunk_index"] = chunkIndex.ToString(),
                ["chunk_id"] = chunkId,
                ["chunking_strategy"] = "sentence"
            };

            chunks.Add(new DocumentChunk(
                chunkId,
                chunkContent,
                chunkIndex,
                startPos,
                startPos + chunkContent.Length,
                documentId,
                chunkMetadata));
        }

        return chunks;
    }

    private List<DocumentChunk> ChunkByParagraph(string text, string documentId, Dictionary<string, string> metadata)
    {
        var chunks = new List<DocumentChunk>();
        var paragraphs = text.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new StringBuilder();
        var chunkIndex = 0;
        var position = 0;

        foreach (var paragraph in paragraphs)
        {
            if (currentChunk.Length + paragraph.Length > _options.MaxChunkSize && currentChunk.Length > 0)
            {
                // Create chunk
                var chunkContent = currentChunk.ToString().Trim();
                var chunkId = $"{documentId}-chunk-{chunkIndex:D4}";
                
                var chunkMetadata = new Dictionary<string, string>(metadata)
                {
                    ["chunk_index"] = chunkIndex.ToString(),
                    ["chunk_id"] = chunkId,
                    ["chunking_strategy"] = "paragraph"
                };

                chunks.Add(new DocumentChunk(
                    chunkId,
                    chunkContent,
                    chunkIndex,
                    position - chunkContent.Length,
                    position,
                    documentId,
                    chunkMetadata));

                currentChunk.Clear();
                chunkIndex++;
            }

            currentChunk.Append(paragraph).Append("\n\n");
            position += paragraph.Length + 2;
        }

        // Add remaining content
        if (currentChunk.Length > 0)
        {
            var chunkContent = currentChunk.ToString().Trim();
            var chunkId = $"{documentId}-chunk-{chunkIndex:D4}";
            
            var chunkMetadata = new Dictionary<string, string>(metadata)
            {
                ["chunk_index"] = chunkIndex.ToString(),
                ["chunk_id"] = chunkId,
                ["chunking_strategy"] = "paragraph"
            };

            chunks.Add(new DocumentChunk(
                chunkId,
                chunkContent,
                chunkIndex,
                position - chunkContent.Length,
                position,
                documentId,
                chunkMetadata));
        }

        return chunks;
    }

    private List<(string Content, int Length)> SplitTextRecursively(string text, string[] separators, int separatorIndex)
    {
        if (text.Length <= _options.MaxChunkSize)
        {
            return [(text, text.Length)];
        }

        if (separatorIndex >= separators.Length)
        {
            // No more separators, split by character
            var chunks = new List<(string, int)>();
            for (var i = 0; i < text.Length; i += _options.MaxChunkSize)
            {
                var chunkSize = Math.Min(_options.MaxChunkSize, text.Length - i);
                chunks.Add((text.Substring(i, chunkSize), chunkSize));
            }
            return chunks;
        }

        var separator = separators[separatorIndex];
        var parts = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<(string, int)>();
        var currentChunk = new StringBuilder();

        foreach (var part in parts)
        {
            var partWithSeparator = part + separator;
            
            if (currentChunk.Length + partWithSeparator.Length <= _options.MaxChunkSize)
            {
                currentChunk.Append(partWithSeparator);
            }
            else
            {
                if (currentChunk.Length > 0)
                {
                    result.Add((currentChunk.ToString(), currentChunk.Length));
                    currentChunk.Clear();
                }

                if (partWithSeparator.Length > _options.MaxChunkSize)
                {
                    // Recursively split this part
                    var subChunks = SplitTextRecursively(partWithSeparator, separators, separatorIndex + 1);
                    result.AddRange(subChunks);
                }
                else
                {
                    currentChunk.Append(partWithSeparator);
                }
            }
        }

        if (currentChunk.Length > 0)
        {
            result.Add((currentChunk.ToString(), currentChunk.Length));
        }

        return result;
    }

    private List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var sentencePattern = @"[.!?]+\s+";
        var matches = Regex.Split(text, sentencePattern, RegexOptions.IgnoreCase);
        
        for (var i = 0; i < matches.Length - 1; i++)
        {
            if (!string.IsNullOrWhiteSpace(matches[i]))
            {
                sentences.Add(matches[i].Trim() + ".");
            }
        }
        
        // Add the last part if it doesn't end with punctuation
        if (matches.Length > 0 && !string.IsNullOrWhiteSpace(matches[^1]))
        {
            sentences.Add(matches[^1].Trim());
        }

        return sentences;
    }
}