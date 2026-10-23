namespace BlockoutStudio.Services;

/// <summary>
/// Strongly-typed string struct representing the project file type, extension, and default file name.
/// </summary>
public readonly struct ProjectFileType : IEquatable<ProjectFileType>, IComparable<ProjectFileType>
{
    /// <summary>Default file extension without leading dot ("pjblock").</summary>
    public const string DefaultExtension = "pjblock";

    /// <summary>Default file extension with leading dot (".pjblock").</summary>
    public const string DefaultDotExtension = ".pjblock";

    /// <summary>Default file name including the pjblock extension ("Level_Blockout.pjblock").</summary>
    public const string DefaultFileName = "Level_Blockout.pjblock";

    /// <summary>Default instance of the <see cref="ProjectFileType"/> struct.</summary>
    public static readonly ProjectFileType Default = new(DefaultFileName);

    private readonly string _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectFileType"/> struct with a string value.
    /// </summary>
    /// <param name="value">The string value representing the file type or file name.</param>
    public ProjectFileType(string? value)
    {
        _value = string.IsNullOrWhiteSpace(value) ? DefaultFileName : value;
    }

    /// <summary>
    /// Gets the string value of this project file type. Defaults to <see cref="DefaultFileName"/> if uninitialized.
    /// </summary>
    public string Value => _value ?? DefaultFileName;

    /// <summary>
    /// Implicitly converts a <see cref="ProjectFileType"/> struct to its underlying <see cref="string"/> value.
    /// </summary>
    /// <param name="fileType">The project file type struct instance.</param>
    public static implicit operator string(ProjectFileType fileType) => fileType.Value;

    /// <summary>
    /// Implicitly converts a <see cref="string"/> value to a <see cref="ProjectFileType"/> struct.
    /// </summary>
    /// <param name="value">The string value.</param>
    public static implicit operator ProjectFileType(string? value) => new(value);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <inheritdoc/>
    public bool Equals(ProjectFileType other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ProjectFileType other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <inheritdoc/>
    public int CompareTo(ProjectFileType other) => string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(ProjectFileType left, ProjectFileType right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(ProjectFileType left, ProjectFileType right) => !left.Equals(right);
}

