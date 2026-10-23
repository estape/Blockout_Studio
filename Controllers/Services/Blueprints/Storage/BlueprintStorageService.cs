using System.Text.Json;
using BlockoutStudio.Services.Assets;
using BlockoutStudio.Services.Blueprints.Models;
using BlockoutStudio.Services.Blueprints.Transpiler;

namespace BlockoutStudio.Services.Blueprints.Storage;

/// <summary>
/// Service managing filesystem storage, asset tracking, and C# source file emission
/// for visual Blueprint assets.
/// </summary>
public class BlueprintStorageService : IBlueprintStorageService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IBlueprintTranspiler _transpiler;

    public BlueprintStorageService(IBlueprintTranspiler transpiler)
    {
        _transpiler = transpiler;
    }

    public Task<List<BlueprintAssetInfo>> ListBlueprintsAsync(string projectRoot)
    {
        var results = new List<BlueprintAssetInfo>();
        var bpDir = Path.Combine(projectRoot, "Assets", "Blueprints");

        if (!Directory.Exists(bpDir))
        {
            Directory.CreateDirectory(bpDir);
            return Task.FromResult(results);
        }

        var files = Directory.GetFiles(bpDir, "*.bprint", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            try
            {
                var fi = new FileInfo(file);
                var meta = AssetMeta.LoadOrCreate(file);
                var relPath = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                var bpName = Path.GetFileNameWithoutExtension(file);

                var graphType = "Actor";
                try
                {
                    var text = File.ReadAllText(file);
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("Type", out var typeEl))
                    {
                        graphType = typeEl.GetString() ?? "Actor";
                    }
                }
                catch { }

                results.Add(new BlueprintAssetInfo(
                    Name: bpName,
                    RelativePath: relPath,
                    FullPath: file,
                    Guid: meta.Guid,
                    GraphType: graphType,
                    FileSizeBytes: fi.Length,
                    LastModifiedUtc: fi.LastWriteTimeUtc));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[BlueprintStorage] Failed to index '{file}': {ex.Message}");
            }
        }

        return Task.FromResult(results);
    }

    public async Task<BlueprintGraph?> LoadBlueprintAsync(string projectRoot, string blueprintNameOrRelativePath)
    {
        var targetFile = blueprintNameOrRelativePath;
        if (!File.Exists(targetFile))
        {
            var bpDir = Path.Combine(projectRoot, "Assets", "Blueprints");
            var direct = Path.Combine(bpDir, blueprintNameOrRelativePath);
            if (File.Exists(direct))
            {
                targetFile = direct;
            }
            else if (File.Exists(direct + ".bprint"))
            {
                targetFile = direct + ".bprint";
            }
            else
            {
                var match = Directory.GetFiles(bpDir, $"{blueprintNameOrRelativePath}.bprint", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (match != null) targetFile = match;
            }
        }

        if (!File.Exists(targetFile))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(targetFile);
        return JsonSerializer.Deserialize<BlueprintGraph>(json, JsonOpts);
    }

    public async Task<SaveBlueprintResult> SaveBlueprintAsync(string projectRoot, BlueprintGraph graph)
    {
        try
        {
            var bpDir = Path.Combine(projectRoot, "Assets", "Blueprints");
            var genDir = Path.Combine(projectRoot, "Assets", "Scripts", "Generated");

            if (!Directory.Exists(bpDir)) Directory.CreateDirectory(bpDir);
            if (!Directory.Exists(genDir)) Directory.CreateDirectory(genDir);

            var fileName = $"{graph.Name}.bprint";
            var bpPath = Path.Combine(bpDir, fileName);

            // 1. Serialize Graph JSON
            var json = JsonSerializer.Serialize(graph, JsonOpts);
            await File.WriteAllTextAsync(bpPath, json);

            // 2. Manage .meta sidecar
            var meta = AssetMeta.LoadOrCreate(bpPath);
            meta.AssetType = "Blueprint";
            meta.SourceHash = AssetMeta.ComputeFileSha256(bpPath);
            meta.LastModifiedUtc = DateTime.UtcNow.ToString("o");
            var metaPath = bpPath + ".meta";
            await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(meta, JsonOpts));

            // 3. Transpile and emit C# Source
            var csCode = _transpiler.TranspileToCSharp(graph);
            var csPath = Path.Combine(genDir, $"{graph.Name}.cs");
            await File.WriteAllTextAsync(csPath, csCode);

            return new SaveBlueprintResult(true, bpPath, csPath);
        }
        catch (Exception ex)
        {
            return new SaveBlueprintResult(false, string.Empty, string.Empty, ex.Message);
        }
    }

    public Task<bool> DeleteBlueprintAsync(string projectRoot, string blueprintName)
    {
        try
        {
            var bpDir = Path.Combine(projectRoot, "Assets", "Blueprints");
            var bpPath = Path.Combine(bpDir, $"{blueprintName}.bprint");
            var metaPath = bpPath + ".meta";
            var csPath = Path.Combine(projectRoot, "Assets", "Scripts", "Generated", $"{blueprintName}.cs");

            if (File.Exists(bpPath)) File.Delete(bpPath);
            if (File.Exists(metaPath)) File.Delete(metaPath);
            if (File.Exists(csPath)) File.Delete(csPath);

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
