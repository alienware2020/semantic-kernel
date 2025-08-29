using Microsoft.SemanticKernel;

namespace WebAPI.Interfaces;

public interface ISemanticKernelService
{
    Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default);
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}