using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Application.Chat.Commands;
using Rag.Application.Chat.Queries;
using Rag.Application.Documents.Commands;
using Rag.Application.Documents.Queries;
using Rag.Application.Ingestion;
using Rag.Application.Options;

namespace Rag.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

        services.AddScoped<UploadDocumentHandler>();
        services.AddScoped<ReplaceDocumentFileHandler>();
        services.AddScoped<UpdateDocumentInfoHandler>();
        services.AddScoped<DeleteDocumentHandler>();
        services.AddScoped<RetryIndexingHandler>();
        services.AddScoped<ReindexAllHandler>();
        services.AddScoped<GetDocumentsHandler>();
        services.AddScoped<GetDocumentHandler>();
        services.AddScoped<GetDocumentStatusHandler>();
        services.AddScoped<DownloadDocumentHandler>();

        services.AddScoped<AskQuestionHandler>();
        services.AddScoped<DeleteSessionHandler>();
        services.AddScoped<GetMySessionsHandler>();
        services.AddScoped<GetSessionMessagesHandler>();

        services.AddScoped<IngestionPipeline>();

        return services;
    }
}
