using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TextGeneration;
using WebAPI.Interfaces;
using WebAPI.Models;

namespace WebAPI.Services;

public class SemanticKernelService : ISemanticKernelService
{
    private readonly Kernel _kernel;
    private readonly ITextGenerationService _textGenerationService;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
    private readonly OpenAIOptions _openAiOptions;

    public SemanticKernelService(Kernel kernel, IOptions<OpenAIOptions> openAIOptions)
    {
        _openAiOptions = openAIOptions.Value;

        // Use lazy initialization to defer kernel creation until first use
        _kernel = kernel;
        _textGenerationService = _kernel.GetRequiredService<ITextGenerationService>();
        _embeddingService = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    }

    // public SemanticKernelService(IKernelFactory kernelFactory, IOptions<OpenAIOptions> openAIOptions)
    // {
    //     _openAiOptions = openAIOptions.Value;
    //
    //     // Use lazy initialization to defer kernel creation until first use
    //     _kernel = new Lazy<Kernel>(kernelFactory.CreateKernel);
    //     Console.WriteLine("SemanticKernelService initialized.");
    //     _textGenerationService =
    //         new Lazy<ITextGenerationService>(() => _kernel.Value.GetRequiredService<ITextGenerationService>());
    //     _embeddingService =
    //         new Lazy<IEmbeddingGenerator<string, Embedding<float>>>(() =>
    //             _kernel.Value.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());
    // }

    public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            MaxTokens = _openAiOptions.MaxTokens,
            Temperature = _openAiOptions.Temperature
        };

        var result = await _textGenerationService.GetTextContentAsync(
            prompt,
            executionSettings,
            _kernel,
            cancellationToken);

        return result.Text;
    }

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text,
        CancellationToken cancellationToken = default)
    {
        var embeddings = await _embeddingService.GenerateAsync([text], cancellationToken: cancellationToken);

        return embeddings.FirstOrDefault()?.Vector ?? ReadOnlyMemory<float>.Empty;
    }
}