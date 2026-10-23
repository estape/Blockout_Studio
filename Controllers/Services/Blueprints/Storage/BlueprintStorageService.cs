using System.Text.Json;
using BlockoutStudio.Controllers.Services.Blueprints.Converters;
using BlockoutStudio.Controllers.Services.Blueprints.Serialization;
using BlockoutStudio.Controllers.Services.Blueprints.Validation;
using BlockoutStudio.Models.Blueprints.Format;
using BlockoutStudio.Services.Assets;
using BlockoutStudio.Models.Blueprints;
using BlockoutStudio.Services.Blueprints.Transpiler;

namespace BlockoutStudio.Services.Blueprints.Storage;

/// <summary>
/// Service managing filesystem storage, asset tracking, and C# source file emission
/// for visual Blueprint assets (.bprint and .bgraph).
/// </summary>
public class BlueprintStorageService : IBlueprintStorageService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IBlueprintTranspiler _transpiler;
    private readonly IGraphSerializer _graphSerializer;
    private readonly IGraphValidator _graphValidator;

    public BlueprintStorageService(
        IBlueprintTranspiler transpiler,
        IGraphSerializer? graphSerializer = null,
        IGraphValidator? graphValidator = null)
    {
        _transpiler = transpiler;
        _graphSerializer = graphSerializer ?? new GraphSerializer();
        _graphValidator = graphValidator ?? new GraphValidator();
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

        // Search for both .bprint and .bgraph files
        var bprintFiles = Directory.GetFiles(bpDir, "*.bprint", SearchOption.AllDirectories);
        var bgraphFiles = Directory.GetFiles(bpDir, "*.bgraph", SearchOption.AllDirectories);
        var allFiles = bprintFiles.Concat(bgraphFiles);

        foreach (var file in allFiles)
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
                    else if (doc.RootElement.TryGetProperty("targetActorClass", out var targetActorEl))
                    {
                        graphType = targetActorEl.GetString() ?? "Actor";
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
            else if (File.Exists(direct + ".bgraph"))
            {
                // Load .bgraph and convert to BlueprintGraph
                var doc = await LoadGraphAsync(projectRoot, blueprintNameOrRelativePath);
                return doc != null ? GraphFormatConverter.ToBlueprintGraph(doc) : null;
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

        if (targetFile.EndsWith(".bgraph", StringComparison.OrdinalIgnoreCase))
        {
            var doc = await LoadGraphAsync(projectRoot, targetFile);
            return doc != null ? GraphFormatConverter.ToBlueprintGraph(doc) : null;
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

    public async Task<GraphDocument?> LoadGraphAsync(string projectRoot, string graphNameOrRelativePath)
    {
        var targetFile = graphNameOrRelativePath;
        if (!File.Exists(targetFile))
        {
            var bpDir = Path.Combine(projectRoot, "Assets", "Blueprints");
            var direct = Path.Combine(bpDir, graphNameOrRelativePath);
            if (File.Exists(direct))
            {
                targetFile = direct;
            }
            else if (File.Exists(direct + ".bgraph"))
            {
                targetFile = direct + ".bgraph";
            }
            else if (File.Exists(direct + ".bprint"))
            {
                var bp = await LoadBlueprintAsync(projectRoot, graphNameOrRelativePath);
                return bp != null ? GraphFormatConverter.ToGraphDocument(bp) : null;
            }
            else
            {
                var match = Directory.GetFiles(bpDir, $"{graphNameOrRelativePath}.bgraph", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (match != null) targetFile = match;
            }
        }

        if (!File.Exists(targetFile))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(targetFile);
        return _graphSerializer.Deserialize(json);
    }

    public async Task<SaveBlueprintResult> SaveGraphAsync(string projectRoot, GraphDocument document)
    {
        try
        {
            // 1. Validate Document first
            var validation = _graphValidator.Validate(document);
            if (!validation.IsValid)
            {
                var firstErr = validation.Errors[0];
                return new SaveBlueprintResult(false, string.Empty, string.Empty, $"Validation failed [{firstErr.Code}]: {firstErr.Message}");
            }

            var bpDir = Path.Combine(projectRoot, "Assets", "Blueprints");
            var genDir = Path.Combine(projectRoot, "Assets", "Scripts", "Generated");

            if (!Directory.Exists(bpDir)) Directory.CreateDirectory(bpDir);
            if (!Directory.Exists(genDir)) Directory.CreateDirectory(genDir);

            var fileName = $"{document.Name}.bgraph";
            var bpPath = Path.Combine(bpDir, fileName);

            // 2. Serialize using AOT compile-time source generator
            var json = _graphSerializer.Serialize(document);
            await File.WriteAllTextAsync(bpPath, json);

            // 3. Manage .meta sidecar
            var meta = AssetMeta.LoadOrCreate(bpPath);
            meta.AssetType = "BlueprintGraph";
            meta.SourceHash = AssetMeta.ComputeFileSha256(bpPath);
            meta.LastModifiedUtc = DateTime.UtcNow.ToString("o");
            var metaPath = bpPath + ".meta";
            await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(meta, JsonOpts));

            // 4. Transpile and emit C# Source
            var legacyGraph = GraphFormatConverter.ToBlueprintGraph(document);
            var csCode = _transpiler.TranspileToCSharp(legacyGraph);
            var csPath = Path.Combine(genDir, $"{document.Name}.g.cs");
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
            var bgraphPath = Path.Combine(bpDir, $"{blueprintName}.bgraph");
            var metaPath = bpPath + ".meta";
            var metaGraphPath = bgraphPath + ".meta";
            var csPath = Path.Combine(projectRoot, "Assets", "Scripts", "Generated", $"{blueprintName}.cs");
            var gcsPath = Path.Combine(projectRoot, "Assets", "Scripts", "Generated", $"{blueprintName}.g.cs");

            if (File.Exists(bpPath)) File.Delete(bpPath);
            if (File.Exists(bgraphPath)) File.Delete(bgraphPath);
            if (File.Exists(metaPath)) File.Delete(metaPath);
            if (File.Exists(metaGraphPath)) File.Delete(metaGraphPath);
            if (File.Exists(csPath)) File.Delete(csPath);
            if (File.Exists(gcsPath)) File.Delete(gcsPath);

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
