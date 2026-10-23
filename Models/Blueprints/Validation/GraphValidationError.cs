namespace BlockoutStudio.Models.Blueprints.Validation;

/// <summary>
/// Detailed validation error encountered during pre-save or pre-compilation graph checks.
/// </summary>
public class GraphValidationError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public string? PinId { get; set; }

    public GraphValidationError() { }

    public GraphValidationError(string code, string message, string? nodeId = null, string? pinId = null)
    {
        Code = code;
        Message = message;
        NodeId = nodeId;
        PinId = pinId;
    }
}

/// <summary>
/// Detailed validation warning that does not prevent saving or execution.
/// </summary>
public class GraphValidationWarning
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public string? PinId { get; set; }

    public GraphValidationWarning() { }

    public GraphValidationWarning(string code, string message, string? nodeId = null, string? pinId = null)
    {
        Code = code;
        Message = message;
        NodeId = nodeId;
        PinId = pinId;
    }
}

/// <summary>
/// Well-known error codes for graph validation.
/// </summary>
public static class GraphValidationErrors
{
    public const string SelfConnection = "ERR_SELF_CONNECT";
    public const string NodeNotFound = "ERR_NODE_NOT_FOUND";
    public const string PinNotFound = "ERR_PIN_NOT_FOUND";
    public const string InvalidPinDirection = "ERR_INVALID_PIN_DIRECTION";
    public const string TypeMismatch = "ERR_TYPE_MISMATCH";
    public const string ExecDataMismatch = "ERR_EXEC_DATA_MISMATCH";
    public const string MultipleInputConnections = "ERR_MULTIPLE_INPUT_CONNECTIONS";
    public const string CircularExecutionCycle = "ERR_CIRCULAR_EXECUTION_CYCLE";
    public const string CircularDataDependency = "ERR_CIRCULAR_DATA_DEPENDENCY";
}

