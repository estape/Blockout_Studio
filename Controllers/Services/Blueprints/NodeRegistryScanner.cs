using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BlockoutStudio.Models.Blueprints;
using BlockoutStudio.Services.Blueprints.Runtime;

namespace BlockoutStudio.Services.Blueprints;

/// <summary>
/// Reflection scanner service inspecting engine types for methods decorated with [BlueprintNode],
/// generating strongly-typed node metadata schemas with color-coded pins.
/// </summary>
public partial class NodeRegistryScanner : INodeRegistryScanner
{
    private readonly object _lock = new();
    private NodeCatalog? _cachedCatalog;

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex CamelToSpacedRegex();

    [GeneratedRegex("([A-Z]+)([A-Z][a-z])")]
    private static partial Regex AcronymToSpacedRegex();

    [GeneratedRegex("([a-zA-Z])([0-9])")]
    private static partial Regex AlphaToNumRegex();

    /// <inheritdoc/>
    public NodeCatalog GetCatalog(bool forceRescan = false)
    {
        lock (_lock)
        {
            if (_cachedCatalog != null && !forceRescan)
            {
                return _cachedCatalog;
            }

            var catalog = new NodeCatalog
            {
                ScannedAt = DateTime.UtcNow,
                Version = 1,
                Nodes = [],
                Categories = new(StringComparer.OrdinalIgnoreCase)
            };

            // Scan assemblies in current AppDomain
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && (a.FullName?.StartsWith("BlockoutStudio", StringComparison.OrdinalIgnoreCase) == true))
                .Distinct()
                .ToList();

            // Always ensure the assembly declaring this scanner is included
            var scannerAssembly = typeof(NodeRegistryScanner).Assembly;
            if (!assemblies.Contains(scannerAssembly))
            {
                assemblies.Add(scannerAssembly);
            }

            foreach (var asm in assemblies)
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
                }

                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

                    foreach (var method in methods)
                    {
                        var attr = method.GetCustomAttribute<BlueprintNodeAttribute>();
                        if (attr == null) continue;

                        var nodeDef = BuildNodeDefinition(type, method, attr);
                        catalog.Nodes.Add(nodeDef);

                        var category = string.IsNullOrWhiteSpace(attr.Category) ? "General" : attr.Category;
                        if (!catalog.Categories.TryGetValue(category, out var nodeList))
                        {
                            nodeList = [];
                            catalog.Categories[category] = nodeList;
                        }
                        nodeList.Add(nodeDef.Id);
                    }
                }
            }

            // Inject built-in execution flow nodes (Branch, Sequence, ForEachLoop, Delay, DoOnce)
            InjectBuiltInFlowControlNodes(catalog);

            _cachedCatalog = catalog;
            return catalog;
        }
    }

    private static void InjectBuiltInFlowControlNodes(NodeCatalog catalog)
    {
        const string category = "Flow Control";
        if (!catalog.Categories.TryGetValue(category, out var flowList))
        {
            flowList = [];
            catalog.Categories[category] = flowList;
        }

        var flowNodes = new List<DiscoveredNodeDefinition>
        {
            // Branch
            new()
            {
                Id = "FlowControl.Branch",
                NodeType = "Branch",
                Title = "Branch",
                Category = category,
                IsPure = false,
                DeclaringType = "Engine.FlowControl",
                MethodName = "Branch",
                Signature = "Branch(bool Condition)",
                IsStatic = true,
                Inputs =
                [
                    new DiscoveredNodePin("ExecIn", "Exec", "Input", "#ffffff"),
                    new DiscoveredNodePin("Condition", "Boolean", "Input", "#dc2626", "false")
                ],
                Outputs =
                [
                    new DiscoveredNodePin("True", "Exec", "Output", "#ffffff"),
                    new DiscoveredNodePin("False", "Exec", "Output", "#ffffff")
                ],
                Properties = new() { ["DeclaringTypeShort"] = "FlowControl", ["IsStatic"] = "true" }
            },

            // Sequence
            new()
            {
                Id = "FlowControl.Sequence",
                NodeType = "Sequence",
                Title = "Sequence",
                Category = category,
                IsPure = false,
                DeclaringType = "Engine.FlowControl",
                MethodName = "Sequence",
                Signature = "Sequence()",
                IsStatic = true,
                Inputs =
                [
                    new DiscoveredNodePin("ExecIn", "Exec", "Input", "#ffffff")
                ],
                Outputs =
                [
                    new DiscoveredNodePin("Then 0", "Exec", "Output", "#ffffff"),
                    new DiscoveredNodePin("Then 1", "Exec", "Output", "#ffffff")
                ],
                Properties = new() { ["DeclaringTypeShort"] = "FlowControl", ["IsStatic"] = "true" }
            },

            // ForEachLoop
            new()
            {
                Id = "FlowControl.ForEachLoop",
                NodeType = "ForEachLoop",
                Title = "For Each Loop",
                Category = category,
                IsPure = false,
                DeclaringType = "Engine.FlowControl",
                MethodName = "ForEachLoop",
                Signature = "ForEachLoop(object Array)",
                IsStatic = true,
                Inputs =
                [
                    new DiscoveredNodePin("ExecIn", "Exec", "Input", "#ffffff"),
                    new DiscoveredNodePin("Array", "ActorRef", "Input", "#3b82f6")
                ],
                Outputs =
                [
                    new DiscoveredNodePin("LoopBody", "Exec", "Output", "#ffffff"),
                    new DiscoveredNodePin("ArrayElement", "ActorRef", "Output", "#3b82f6"),
                    new DiscoveredNodePin("ArrayIndex", "Integer", "Output", "#06b6d4", "0"),
                    new DiscoveredNodePin("Completed", "Exec", "Output", "#ffffff")
                ],
                Properties = new() { ["DeclaringTypeShort"] = "FlowControl", ["IsStatic"] = "true" }
            },

            // Delay
            new()
            {
                Id = "FlowControl.Delay",
                NodeType = "Delay",
                Title = "Delay",
                Category = category,
                IsPure = false,
                DeclaringType = "Engine.FlowControl",
                MethodName = "Delay",
                Signature = "Delay(float Duration)",
                IsStatic = true,
                Inputs =
                [
                    new DiscoveredNodePin("ExecIn", "Exec", "Input", "#ffffff"),
                    new DiscoveredNodePin("Duration", "Float", "Input", "#22c55e", "0.2")
                ],
                Outputs =
                [
                    new DiscoveredNodePin("Completed", "Exec", "Output", "#ffffff")
                ],
                Properties = new() { ["DeclaringTypeShort"] = "FlowControl", ["IsStatic"] = "true" }
            },

            // DoOnce
            new()
            {
                Id = "FlowControl.DoOnce",
                NodeType = "DoOnce",
                Title = "Do Once",
                Category = category,
                IsPure = false,
                DeclaringType = "Engine.FlowControl",
                MethodName = "DoOnce",
                Signature = "DoOnce(bool StartClosed)",
                IsStatic = true,
                Inputs =
                [
                    new DiscoveredNodePin("ExecIn", "Exec", "Input", "#ffffff"),
                    new DiscoveredNodePin("Reset", "Exec", "Input", "#ffffff"),
                    new DiscoveredNodePin("StartClosed", "Boolean", "Input", "#dc2626", "false")
                ],
                Outputs =
                [
                    new DiscoveredNodePin("Completed", "Exec", "Output", "#ffffff")
                ],
                Properties = new() { ["DeclaringTypeShort"] = "FlowControl", ["IsStatic"] = "true" }
            }
        };

        foreach (var fn in flowNodes)
        {
            catalog.Nodes.Add(fn);
            flowList.Add(fn.Id);
        }
    }

    private static DiscoveredNodeDefinition BuildNodeDefinition(Type declaringType, MethodInfo method, BlueprintNodeAttribute attr)
    {
        var category = string.IsNullOrWhiteSpace(attr.Category) ? "General" : attr.Category;
        var title = string.IsNullOrWhiteSpace(attr.DisplayName) ? HumanizeMethodName(method.Name) : attr.DisplayName;
        var id = $"{declaringType.Name}.{method.Name}";

        var node = new DiscoveredNodeDefinition
        {
            Id = id,
            NodeType = method.Name,
            Title = title,
            Category = category,
            IsPure = attr.IsPure,
            DeclaringType = declaringType.FullName ?? declaringType.Name,
            MethodName = method.Name,
            IsStatic = method.IsStatic,
            Signature = method.ToString() ?? method.Name,
            Inputs = [],
            Outputs = [],
            Properties = new()
            {
                ["DeclaringTypeShort"] = declaringType.Name,
                ["IsStatic"] = method.IsStatic ? "true" : "false"
            }
        };

        // 1. If Impure, inject ExecIn pin
        if (!attr.IsPure)
        {
            node.Inputs.Add(new DiscoveredNodePin("ExecIn", "Exec", "Input", "#ffffff"));
        }

        // 2. Map Method Parameters to Input Pins
        var parameters = method.GetParameters();
        foreach (var param in parameters)
        {
            var (pinType, color, defaultVal) = MapClrTypeToPin(param.ParameterType);

            // If parameter has a default value defined in C# method signature, use it
            if (param.HasDefaultValue && param.DefaultValue != null)
            {
                defaultVal = FormatLiteralValue(param.DefaultValue, pinType);
            }

            node.Inputs.Add(new DiscoveredNodePin(
                name: param.Name ?? "Param",
                type: pinType,
                direction: "Input",
                color: color,
                defaultValue: defaultVal));
        }

        // 3. If Impure, inject ExecOut pin
        if (!attr.IsPure)
        {
            node.Outputs.Add(new DiscoveredNodePin("ExecOut", "Exec", "Output", "#ffffff"));
        }

        // 4. Map Return Type to Primary Output Data Pin
        if (method.ReturnType != typeof(void))
        {
            var (retType, retColor, _) = MapClrTypeToPin(method.ReturnType);
            node.Outputs.Add(new DiscoveredNodePin(
                name: "ReturnValue",
                type: retType,
                direction: "Output",
                color: retColor,
                defaultValue: null));
        }

        return node;
    }

    private static (string Type, string Color, string? DefaultValue) MapClrTypeToPin(Type type)
    {
        // Unpack Nullable<T>
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(float) || underlying == typeof(double))
        {
            return ("Float", "#22c55e", "0.0");
        }
        if (underlying == typeof(int) || underlying == typeof(short) || underlying == typeof(byte) || underlying == typeof(long))
        {
            return ("Integer", "#06b6d4", "0");
        }
        if (underlying == typeof(bool))
        {
            return ("Boolean", "#dc2626", "false");
        }
        if (underlying == typeof(Vector3))
        {
            return ("Vector3", "#f59e0b", "0,0,0");
        }
        if (underlying == typeof(string))
        {
            return ("String", "#ec4899", "");
        }
        if (typeof(Actor).IsAssignableFrom(underlying) || typeof(ActorBlueprint).IsAssignableFrom(underlying))
        {
            return ("ActorRef", "#3b82f6", null);
        }

        // General object reference fallback
        return ("ActorRef", "#3b82f6", null);
    }

    private static string FormatLiteralValue(object val, string pinType)
    {
        return pinType switch
        {
            "Float" => Convert.ToSingle(val).ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture),
            "Integer" => Convert.ToInt64(val).ToString(),
            "Boolean" => Convert.ToBoolean(val) ? "true" : "false",
            "String" => val.ToString() ?? "",
            "Vector3" => val is Vector3 v ? $"{v.X},{v.Y},{v.Z}" : "0,0,0",
            _ => val.ToString() ?? ""
        };
    }

    private static string HumanizeMethodName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        // Space out PascalCase and camelCase words
        var result = CamelToSpacedRegex().Replace(name, "$1 $2");
        result = AcronymToSpacedRegex().Replace(result, "$1 $2");
        result = AlphaToNumRegex().Replace(result, "$1 $2");

        return result;
    }
}

