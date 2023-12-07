using BitsNo.Helpers;

namespace UpdateChecker;

public static class GrabAndDownload
{
    public static async Task<List<FetchResult>> GetDocuments()
    {
        var documents = new List<FetchResult>();
        var documentUrls = new[] { new Uri("https://www.bits.no/document/iban/") };
        foreach (var u in documentUrls)
        {
            var archiveMetadataTask = WaybackSnapshot.GetArchiveDataNoExceptions(u);
            var srcDataResultTask = DocumentHelpers.FetchToMemoryAsync(u, archiveMetadataTask);

            var doc = await srcDataResultTask.ConfigureAwait(false);
            documents.Add(doc);
            Console.WriteLine($" * Downloaded {u} {doc.Sha1} {doc.Data.Length:#,##0}");
            if (doc.ArchiveMetadata is null ||
                doc.Sha1 == doc.ArchiveMetadata.Digest) continue;
            Console.WriteLine($"{u} new: {doc.Sha1} archived {doc.ArchiveMetadata.Digest}");
            await WaybackSnapshot.RequestSaveAsync(u).ConfigureAwait(false);
            doc.ArchiveMetadata = await WaybackSnapshot.GetArchiveDataNoExceptions(u).ConfigureAwait(false);
        }
        return documents;
    }
}
