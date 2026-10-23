using BlockoutStudio.Models.Blueprints.Format;
using BlockoutStudio.Models.Blueprints.Validation;

namespace BlockoutStudio.Controllers.Services.Blueprints.Validation;

/// <summary>
/// Authoritative validation service enforcing graph topology invariants, strict type-checking,
/// single fan-in constraints, and pre-flight acyclic cycle prevention.
/// </summary>
public interface IGraphValidationService : IGraphValidator
{
    /// <summary>
    /// Pre-flight validation evaluating whether a proposed pin connection satisfies all topology,
    /// direction, data type, and acyclic constraints WITHOUT mutating the underlying document.
    /// </summary>
    GraphValidationResult CanConnectPins(GraphDocument document, string sourceNodeId, string sourcePinId, string targetNodeId, string targetPinId);
}

