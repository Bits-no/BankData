
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
var ghEnvFile = Environment.GetEnvironmentVariable("$GITHUB_ENV");

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

var dateModified = DateTime.MinValue;
var modifiedDocuments = new List<FetchResult>();
foreach (var doc in await documentsTask)
{
    var downloadName = doc.GetFilename();
    oldFilesToRemove.Remove(downloadName);
    Console.Write($"\n Validating {doc} -> {downloadName}");
    var fi = new FileInfo(Path.Combine(diDocCache.FullName, downloadName));

    var fileSha1 = await fi.GetFileSha1Base32Async();
    if (doc.Sha1 != fileSha1)
    {
        var logline = $"* from: {fileSha1} -> {doc.Sha1} to {fi.FullName}";
        Console.WriteLine(logline);
        if (ghStepSummaryFile is not null)
            await File.AppendAllTextAsync(ghStepSummaryFile, $"{logline} src: {doc}\n");
        modifiedDocuments.Add(doc);

        var xlsData = doc.ContentType?.MediaType == "application/vnd.ms-excel"
            ? DocumentExtractor.ParseXls(doc.Data)
            : DocumentExtractor.ParseXlsx(doc.Data);
        var parsedData = string.Join("\n", xlsData.GeneratePsv()) + "\n";
        var sourcePsv = new FileInfo(Path.Combine(diDataDir.FullName, "source.psv"));
        await File.WriteAllTextAsync(sourcePsv.FullName, parsedData);
        Console.WriteLine($"Updated {sourcePsv.FullName} Modified: {xlsData.Modified?.ToUniversalTime():o}");
        if (ghStepSummaryFile is not null)
            await File.AppendAllTextAsync(ghStepSummaryFile, $"Modified: {xlsData.Modified?.ToUniversalTime():o}\nData:\n{parsedData}\n");
        dateModified = xlsData.Modified ?? DateTime.Now;
        if (ghEnvFile is not null)
        {
            await File.AppendAllTextAsync(ghEnvFile, $"DATA_MODIFIED_DATE={xlsData.Modified?.ToUniversalTime():o}\n");
            await File.AppendAllTextAsync(ghEnvFile, $"DATA_VERSION={xlsData.Modified:yyyy'.'m'.d'}\n");
        }

        using (var fs = fi.OpenWrite())
        {
            doc.Data.WriteTo(fs);
            fs.SetLength(doc.Data.Length); // ensure existing files are truncated to correct size
            await fs.FlushAsync();
            fs.Close();
        }

        fi.Refresh();
        fileSha1 = await fi.GetFileSha1Base32Async();
        if (fileSha1 != doc.Sha1)
            throw new Exception($"* {fi.FullName} On-disk hash was {fileSha1} expected {doc.Sha1}, size: {fi.Length} expected {doc.Data.Length}");
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
title: Changes in sourcedata {dateModified.ToUniversalTime():o}
---

* {string.Join("\n* ", modifiedDocuments)}

{actionUrl}

");
}
