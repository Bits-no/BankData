
using BitsNo.Helpers;

Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.CreateSpecificCulture("nb-NO");

DirectoryInfo diDataDir = new(Directory.GetCurrentDirectory());
while (true)
{
    var dataDir = diDataDir.EnumerateDirectories("Data").FirstOrDefault();
    if (dataDir is not null)
    {
        diDataDir = dataDir;
        break;
    }
    if (diDataDir.Parent is null) throw new ArgumentOutOfRangeException();
    diDataDir = diDataDir.Parent!;
}

var diDocCache = Directory.CreateDirectory(".doc_cache");
Console.WriteLine($"Working with {diDocCache.FullName} ...");
var ghStepSummaryFile = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");

var documentsTask = UpdateChecker.GrabAndDownload.GetDocuments();
var originalFiles = diDocCache.EnumerateFiles().ToList();
var oldFilesToRemove = originalFiles.ToDictionary(fi => Path.GetFileName(fi.Name));
var filesWithHash = originalFiles.AsParallel().ToDictionary(fi => fi, DocumentHelpers.GetFileSha1Base32Async);
foreach (var fwh in filesWithHash)
{
    var digest = await fwh.Value.ConfigureAwait(false);
    var fi = fwh.Key;
    Console.WriteLine($"* Existing local file: {fi.Name}\t{fi.Length}\t{digest}");
}

var modifiedDocuments = new List<FetchResult>();
foreach (var doc in await documentsTask)
{
    var downloadName = doc.GetFilename();
    oldFilesToRemove.Remove(downloadName);
    var dldocSha1Task = DocumentHelpers.GetSha1Base32Async(doc.Data);
    Console.Write($"\n Validating {doc} -> {downloadName}");
    var fi = new FileInfo(Path.Combine(diDocCache.FullName, downloadName));

    var fileSha1 = await DocumentHelpers.GetFileSha1Base32Async(fi);
    var dldocSha1 = await dldocSha1Task;
    if (dldocSha1 != fileSha1)
    {
        var logline = $"* from: {fileSha1} -> {dldocSha1} to {fi.FullName}";
        Console.WriteLine(logline);
        if (ghStepSummaryFile is not null)
            await File.AppendAllTextAsync(ghStepSummaryFile, $"{logline} src: {doc.Url}\n");
        modifiedDocuments.Add(doc);

        var parsedData = string.Join("\n", DocumentExtractor.ParseXlsx(doc.Data).GeneratePsv()) + "\n";
        var sourcePsv = new FileInfo(Path.Combine(diDataDir.FullName, "source.psv"));
        await File.WriteAllTextAsync(sourcePsv.FullName, parsedData);
        Console.WriteLine($"Updated {sourcePsv.FullName}");
        if (ghStepSummaryFile is not null)
            await File.AppendAllTextAsync(ghStepSummaryFile, $"Data: {parsedData}\n");

        using (var fs = fi.OpenWrite())
        {
            doc.Data.WriteTo(fs);
            fs.SetLength(doc.Data.Length); // ensure existing files are truncated to correct size
            await fs.FlushAsync();
            fs.Close();
        }

        fi.Refresh();
        fileSha1 = await DocumentHelpers.GetFileSha1Base32Async(fi);
        if (fileSha1 != dldocSha1)
            throw new Exception($"* {fi.FullName} On-disk hash was {fileSha1} expected {dldocSha1}, size: {fi.Length} expected {doc.Data.Length}");
    }
}

foreach (var fi in oldFilesToRemove.Values)
{
    Console.WriteLine($"* Cleanup old file: {Path.GetFileName(fi.Name)}\t{fi.Length}");
    fi.Delete();
}

if (modifiedDocuments.Count != 0)
{
    var fi = new FileInfo("UpdateCheckResultIssue.md");
    Console.WriteLine($"Creating {fi.FullName}");
    var actionUrl = "{{ env.actionurl }}";
    // TODO auto create PR
    File.WriteAllText(fi.FullName,
@$"---
title: Changes in sourcedata {DateTime.Now:yyyy-MM-dd}
---

* {string.Join("\n* ", modifiedDocuments)}

{actionUrl}

");
}
