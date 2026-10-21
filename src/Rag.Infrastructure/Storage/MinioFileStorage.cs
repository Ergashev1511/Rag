using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Rag.Application.Abstractions;
using Rag.Application.Common.Exceptions;
using Rag.Infrastructure.Options;

namespace Rag.Infrastructure.Storage;

internal sealed class MinioFileStorage(IMinioClient client, IOptions<MinioOptions> options) : IFileStorage
{
    private string Bucket => options.Value.Bucket;

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var args = new PutObjectArgs()
            .WithBucket(Bucket)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType);

        await client.PutObjectAsync(args, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        var args = new GetObjectArgs()
            .WithBucket(Bucket)
            .WithObject(key)
            .WithCallbackStream(async (stream, token) => await stream.CopyToAsync(buffer, token));

        try
        {
            await client.GetObjectAsync(args, cancellationToken);
        }
        catch (ObjectNotFoundException)
        {
            await buffer.DisposeAsync();
            throw new NotFoundException("Fayl omborda topilmadi.");
        }

        buffer.Position = 0;
        return buffer;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(Bucket).WithObject(key), cancellationToken);
}
