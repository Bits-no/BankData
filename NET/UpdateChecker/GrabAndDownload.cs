using BitsNo.Helpers;

namespace UpdateChecker;

public static class GrabAndDownload
{
    public static async Task<List<FetchResult>> GetDocuments()
    {
        var documents = new List<FetchResult>();
        WaybackSnapshot? lastSnap = null;
        var documentUrls = new[] { new Uri("https://www.bits.no/document/iban/") };
        foreach (var u in documentUrls)
        {
            var mime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var archiveMetadataTask = WaybackSnapshot.GetArchiveData(u, mime);
            var srcDataResultTask = DocumentHelpers.FetchToMemoryAsync(u);
            var archiveMetadata = await archiveMetadataTask;
            if (lastSnap is null ||
                archiveMetadata?.Timestamp > lastSnap.Timestamp) lastSnap = archiveMetadata;

            var doc = await srcDataResultTask;
            documents.Add(doc);
            var srcSha1 = await DocumentHelpers.GetSha1Base32Async(doc.Data);
            Console.WriteLine($" * Downloaded {u} {srcSha1} {doc.Data.Length:#,##0}");
            if (srcSha1 == archiveMetadata?.Digest)
                continue;
            doc.NewDownload = true;
            Console.WriteLine($"{u} src: {srcSha1} archived {archiveMetadata?.Digest}");
            await WaybackSnapshot.RequestSaveAsync(u);
        }
        return documents;
    }
}
