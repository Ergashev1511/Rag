using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Rag.Infrastructure.Options;

namespace Rag.Infrastructure.Storage;

internal sealed class MinioHealthCheck(IMinioClient client, IOptions<MinioOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(options.Value.Bucket), cancellationToken);
            return exists ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy($"Bucket '{options.Value.Bucket}' topilmadi.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MinIO ga ulanib bo'lmadi.", ex);
        }
    }
}
