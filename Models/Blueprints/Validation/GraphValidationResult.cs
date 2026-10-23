namespace BlockoutStudio.Models.Blueprints.Validation;

/// <summary>
/// Aggregated validation outcome reporting errors and warnings for a graph document.
/// </summary>
public class GraphValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<GraphValidationError> Errors { get; set; } = [];
    public List<GraphValidationWarning> Warnings { get; set; } = [];

    public void AddError(string code, string message, string? nodeId = null, string? pinId = null)
    {
        Errors.Add(new GraphValidationError(code, message, nodeId, pinId));
    }

    public void AddWarning(string code, string message, string? nodeId = null, string? pinId = null)
    {
        Warnings.Add(new GraphValidationWarning(code, message, nodeId, pinId));
    }
}

