using System.Text.Json;
using Photino.NET;
using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// IPC handler for updating or querying the active engine scalability tier and quality presets.
/// </summary>
public class ScalabilityTierHandler : IIpcActionHandler
{
    /// <summary>Asset database service instance.</summary>
    private readonly IAssetDatabaseService _assetDb;

    /// <summary>In-memory cached quality presets.</summary>
    private static QualitySettings _currentQuality = new();

    /// <summary>JSON serialization configuration.</summary>
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <inheritdoc/>
    public string Action => "set_scalability_tier";

    /// <summary>Initializes handler with the asset database service.</summary>
    public ScalabilityTierHandler(IAssetDatabaseService assetDb)
    {
        _assetDb = assetDb;
    }

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        string action = root.TryGetProperty("action", out var act) ? act.GetString() ?? "set_scalability_tier" : "set_scalability_tier";

        if (action == "get_scalability_tiers")
        {
            SendTiersResponse(window);
            return Task.CompletedTask;
        }

        // Set active tier by index (0..3) or by name
        if (payload.TryGetProperty("tierIndex", out var tip))
        {
            int idx = tip.GetInt32();
            if (idx >= 0 && idx < _currentQuality.Tiers.Count)
            {
                _currentQuality.ActiveTierIndex = idx;
                _currentQuality.ActiveTier = _currentQuality.Tiers[idx].TierName;
            }
        }
        else if (payload.TryGetProperty("tierName", out var tnp))
        {
            string name = tnp.GetString() ?? "";
            var match = _currentQuality.Tiers.FirstOrDefault(t => t.TierName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                _currentQuality.ActiveTier = match.TierName;
                _currentQuality.ActiveTierIndex = match.TierIndex;
            }
        }

        // Try saving to active project's ProjectSettings/QualityTiers.json if available
        try
        {
            if (_assetDb.CurrentProjectRoot != null)
            {
                var settingsPath = Path.Combine(_assetDb.CurrentProjectRoot, "ProjectSettings", "QualityTiers.json");
                File.WriteAllText(settingsPath, JsonSerializer.Serialize(_currentQuality, JsonOpts));
            }
        }
        catch { }

        SendTiersResponse(window);
        return Task.CompletedTask;
    }

    /// <summary>Sends current scalability tier preset definitions over the IPC channel.</summary>
    private static void SendTiersResponse(PhotinoWindow window)
    {
        var activePreset = _currentQuality.Tiers.FirstOrDefault(t => t.TierIndex == _currentQuality.ActiveTierIndex)
            ?? _currentQuality.Tiers[2];

        window.SendWebMessage(JsonSerializer.Serialize(new
        {
            type = "scalability_tiers_response",
            success = true,
            activeTier = _currentQuality.ActiveTier,
            activeTierIndex = _currentQuality.ActiveTierIndex,
            activePreset,
            tiers = _currentQuality.Tiers
        }));
    }
}

/// <summary>
/// IPC handler for retrieving the list of available scalability presets and active tier.
/// </summary>
public class GetScalabilityTiersHandler : IIpcActionHandler
{
    /// <summary>Underlying tier handler.</summary>
    private readonly ScalabilityTierHandler _setter;

    /// <inheritdoc/>
    public string Action => "get_scalability_tiers";

    /// <summary>Initializes handler with the setter handler instance.</summary>
    public GetScalabilityTiersHandler(ScalabilityTierHandler setter)
    {
        _setter = setter;
    }

    /// <inheritdoc/>
    public Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        return _setter.HandleAsync(payload, root, window);
    }
}
