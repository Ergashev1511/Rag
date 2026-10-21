using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rag.Application.Abstractions;
using Rag.Application.Ingestion;
using Rag.Domain.Enums;

namespace Rag.Infrastructure.Background;

internal sealed class IngestionWorker(
    ChannelIngestionQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<IngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueUnfinishedAsync(stoppingToken);

        await foreach (var versionId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IngestionPipeline>().ProcessAsync(versionId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Versiya {VersionId} ni qayta ishlashda kutilmagan xato", versionId);
            }
        }
    }

    private async Task RequeueUnfinishedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            var versionIds = await db.DocumentVersions
                .Where(v => v.Status == VersionStatus.Pending || v.Status == VersionStatus.Processing)
                .OrderBy(v => v.CreatedAt)
                .Select(v => v.Id)
                .ToListAsync(cancellationToken);

            foreach (var id in versionIds)
                await queue.EnqueueAsync(id, cancellationToken);

            if (versionIds.Count > 0)
                logger.LogInformation("{Count} ta tugallanmagan versiya qayta navbatga qo'yildi", versionIds.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Tugallanmagan versiyalarni navbatga qo'yib bo'lmadi");
        }
    }
}
