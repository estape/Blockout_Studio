namespace BlockoutStudio.Models.Blueprints;

/// <summary>
/// Marks a C# method for dynamic discovery by the Blueprint reflection scanner.
/// Automatically exposes the method as an actionable visual script node in the editor.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class BlueprintNodeAttribute : Attribute
{
    /// <summary>Visual category for grouping in the context action search menu (e.g. "Math", "Actor", "Audio").</summary>
    public string Category { get; }

    /// <summary>Human-readable display name. If empty, the method name is automatically humanized.</summary>
    public string DisplayName { get; }

    /// <summary>
    /// Indicates whether the node is a pure function.
    /// Pure nodes have no execution pins and evaluate on-demand.
    /// Impure nodes (false) automatically receive ExecIn and ExecOut pins.
    /// </summary>
    public bool IsPure { get; }

    public BlueprintNodeAttribute(string category, string displayName = "", bool isPure = false)
    {
        Category = category;
        DisplayName = displayName;
        IsPure = isPure;
    }
}
