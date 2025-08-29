using Microsoft.SemanticKernel;
using WebAPI.Interfaces;
using WebAPI.Models;
using WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenAIChatCompletion(modelId: "gpt-4o", apiKey: builder.Configuration["OpenAI:ApiKey"]);
#pragma warning disable SKEXP0010
builder.Services.AddOpenAIEmbeddingGenerator(modelId: "text-embedding-3-small",
    apiKey: builder.Configuration["OpenAI:ApiKey"]);
#pragma warning restore SKEXP0010

builder.Services.AddTransient((serviceProvider)=> {
    // var pluginCollection = serviceProvider.GetRequiredService<KernelPluginCollection>();

    return new Kernel(serviceProvider);
});

// Add configuration options
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection(OpenAIOptions.SectionName));
builder.Services.Configure<PineconeOptions>(builder.Configuration.GetSection(PineconeOptions.SectionName));
builder.Services.Configure<ChunkingOptions>(builder.Configuration.GetSection("Chunking"));

// Add services to the container
builder.Services.AddSingleton<ISemanticKernelService, SemanticKernelService>();
builder.Services.AddSingleton<IVectorStoreService, PineconeVectorStoreService>();
builder.Services.AddScoped<IDocumentChunkingService, DocumentChunkingService>();
builder.Services.AddScoped<IRAGService, RAGService>();

// Add HTTP client for external API calls
builder.Services.AddHttpClient();

// Add controllers and API explorer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();