using System.Net.Http.Headers;
using System.Net.Mime;
using System.Security.Cryptography;

namespace BitsNo.Helpers;

public static class DocumentHelpers
{
    public static readonly HttpClient HttpClient = new();

    public static async Task<string> GetSha1Base32Async(this Stream s)
    {
        s.Position = 0;
        return Base32.FromBytes(await SHA1.Create().ComputeHashAsync(s));
    }

    public static async Task<string?> GetFileSha1Base32Async(this FileInfo fi)
    {
        if (!fi.Exists)
            return null;

        using var fs = fi.OpenRead();
        return await fs.GetSha1Base32Async();
    }

    public static async Task<FetchResult> FetchToMemoryAsync(Uri url, Task<WaybackSnapshot?> archiveMetadataTask, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var c = response.Content;
        using var stream = await c.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;
        return new FetchResult(ms, url, c.Headers, 
            await ms.GetSha1Base32Async().ConfigureAwait(false), 
            await archiveMetadataTask.ConfigureAwait(false));
    }
}

public class FetchResult
{
    public Uri Url { get; private set; }
    public MemoryStream Data { get; private set; }
    public string Sha1 { get; private set; }
    public WaybackSnapshot? ArchiveMetadata { get; internal set; }

    public readonly HttpContentHeaders Headers;

    public MediaTypeHeaderValue? ContentType => Headers.ContentType;
    public readonly string? DispositionFilename;

    public FetchResult(MemoryStream data, Uri url, HttpContentHeaders headers, string srcSha1, WaybackSnapshot? archiveMetadata)
    {
        Data = data;
        Url = url;
        Headers = headers;
        Sha1 = srcSha1;
        ArchiveMetadata = archiveMetadata;
        DispositionFilename = Headers.ContentDisposition?.FileName;
        if (DispositionFilename is null)
        {
            try
            {
                var contentDispositionSrc = Headers.GetValues("Content-Disposition").FirstOrDefault() ?? "";
                // Must be Quoted
                if (!contentDispositionSrc.Contains("=\""))
                {
                    contentDispositionSrc = contentDispositionSrc.Replace("filename=", "filename=\"") + "\"";
                }
                var parsedContentDisposition = new ContentDisposition(contentDispositionSrc);
                DispositionFilename = parsedContentDisposition.FileName;
            }
            catch
            {
            }
        }
    }

    public string GetFilename()
    {
        var filename = Path.GetFileName(DispositionFilename ?? Url.LocalPath);
        if (string.IsNullOrEmpty(filename))
            throw new ArgumentException($"Filename empty {Url}");
        return filename;
    }

    public Uri UrlPreferArchive => ArchiveMetadata?.Digest == Sha1 ? ArchiveMetadata.DataUrl : Url;

    public override string ToString() => $"{UrlPreferArchive} {Data.Length} {Sha1}";
}
