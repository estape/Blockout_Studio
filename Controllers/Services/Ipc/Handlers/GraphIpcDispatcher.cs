using System.Text.Json;
using BlockoutStudio.Controllers.Blueprints;
using BlockoutStudio.Controllers.Services.Blueprints.Serialization;
using BlockoutStudio.Models.Blueprints.Format;
using BlockoutStudio.Services.Assets;
using BlockoutStudio.Services.Ipc;
using Photino.NET;

namespace BlockoutStudio.Services.Ipc.Handlers;

/// <summary>
/// Unified IPC dispatcher routing granular graph manipulation events from the Photino webview
/// to the authoritative backend GraphController, enforcing the MVC boundary.
/// </summary>
public sealed class GraphIpcDispatcher : IIpcActionHandler
{
    private readonly IGraphController _controller;
    private readonly IAssetDatabaseService _assetDb;

    public GraphIpcDispatcher(IGraphController controller, IAssetDatabaseService assetDb)
    {
        _controller = controller;
        _assetDb = assetDb;
    }

    public string Action => "graph_action";

    public async Task HandleAsync(JsonElement payload, JsonElement root, PhotinoWindow window)
    {
        var subAction = payload.TryGetProperty("subAction", out var sap) ? sap.GetString() : null;
        var projectRoot = _assetDb.CurrentProjectRoot ?? Directory.GetCurrentDirectory();

        switch (subAction)
        {
            case "graph_create":
            {
                var name = payload.TryGetProperty("name", out var np) ? np.GetString() ?? "NewGraph" : "NewGraph";
                var targetActor = payload.TryGetProperty("targetActorClass", out var tap) ? tap.GetString() ?? "Actor" : "Actor";
                var res = _controller.CreateGraph(name, targetActor);
                Reply(window, "graph_create_response", res, GraphJsonContext.Default.GraphOperationResultGraphDocument);
                break;
            }

            case "graph_load":
            {
                var name = payload.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
                var res = await _controller.LoadGraphAsync(projectRoot, name);
                Reply(window, "graph_load_response", res, GraphJsonContext.Default.GraphOperationResultGraphDocument);
                break;
            }

            case "graph_save":
            {
                var res = await _controller.SaveGraphAsync(projectRoot);
                Reply(window, "graph_save_response", res, GraphJsonContext.Default.GraphOperationResultString);
                break;
            }

            case "graph_add_node":
            {
                var id = payload.TryGetProperty("id", out var idp) ? idp.GetString() : null;
                var methodId = payload.TryGetProperty("targetMethodIdentifier", out var mp) ? mp.GetString() ?? "" : "";
                var nodeType = GraphNodeType.ImpureFunction;
                if ((payload.TryGetProperty("nodeType", out var ntp) && Enum.TryParse<GraphNodeType>(ntp.GetString(), ignoreCase: true, out var parsedType)) ||
                    (payload.TryGetProperty("nodeKind", out var kp) && Enum.TryParse<GraphNodeType>(kp.GetString(), ignoreCase: true, out parsedType)))
                {
                    nodeType = parsedType;
                }
                var x = payload.TryGetProperty("x", out var xp) ? xp.GetSingle() : 0f;
                var y = payload.TryGetProperty("y", out var yp) ? yp.GetSingle() : 0f;

                var res = _controller.AddNode(methodId, nodeType, x, y);
                if (res.Success && res.Data != null)
                {
                    if (!string.IsNullOrEmpty(id)) res.Data.Id = id;
                    if (payload.TryGetProperty("pins", out var pinsEl) && pinsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var pinEl in pinsEl.EnumerateArray())
                        {
                            var pid = pinEl.TryGetProperty("id", out var pidp) ? pidp.GetString() ?? "" : "";
                            var pname = pinEl.TryGetProperty("name", out var pnp) ? pnp.GetString() ?? "" : "";
                            var pdirStr = pinEl.TryGetProperty("direction", out var pdp) ? pdp.GetString() ?? "Input" : "Input";
                            var ptypeStr = pinEl.TryGetProperty("type", out var ptp) ? ptp.GetString() ?? "Exec" : "Exec";
                            var pdir = Enum.TryParse<GraphPinDirection>(pdirStr, true, out var pd) ? pd : GraphPinDirection.Input;
                            var ptype = Enum.TryParse<GraphPinType>(ptypeStr, true, out var pt) ? pt : GraphPinType.Exec;
                            res.Data.Pins.Add(new GraphPin(pid, pname, pdir, ptype));
                        }
                    }
                }
                Reply(window, "graph_add_node_response", res, GraphJsonContext.Default.GraphOperationResultGraphNode);
                break;
            }

            case "graph_remove_node":
            {
                var nodeId = payload.TryGetProperty("nodeId", out var np) ? np.GetString() ?? "" : "";
                var res = _controller.RemoveNode(nodeId);
                Reply(window, "graph_remove_node_response", res, GraphJsonContext.Default.GraphOperationResultBoolean);
                break;
            }

            case "graph_connect_pins":
            {
                var srcNodeId = payload.TryGetProperty("sourceNodeId", out var sn) ? sn.GetString() ?? "" : "";
                var srcPinId = payload.TryGetProperty("sourcePinId", out var sp) ? sp.GetString() ?? "" : "";
                var tgtNodeId = payload.TryGetProperty("targetNodeId", out var tn) ? tn.GetString() ?? "" : "";
                var tgtPinId = payload.TryGetProperty("targetPinId", out var tp) ? tp.GetString() ?? "" : "";

                var res = _controller.ConnectPins(srcNodeId, srcPinId, tgtNodeId, tgtPinId);
                Reply(window, "graph_connect_pins_response", res, GraphJsonContext.Default.GraphOperationResultConnectPinsResponse);
                break;
            }

            case "graph_disconnect_pins":
            {
                var linkId = payload.TryGetProperty("linkId", out var lp) ? lp.GetString() ?? "" : "";
                var res = _controller.DisconnectPins(linkId);
                Reply(window, "graph_disconnect_pins_response", res, GraphJsonContext.Default.GraphOperationResultBoolean);
                break;
            }

            case "graph_set_pin_value":
            {
                var nodeId = payload.TryGetProperty("nodeId", out var np) ? np.GetString() ?? "" : "";
                var pinId = payload.TryGetProperty("pinId", out var pp) ? pp.GetString() ?? "" : "";
                var val = payload.TryGetProperty("value", out var vp) ? vp.GetString() ?? "" : "";

                var res = _controller.SetPinValue(nodeId, pinId, val);
                Reply(window, "graph_set_pin_value_response", res, GraphJsonContext.Default.GraphOperationResultBoolean);
                break;
            }

            case "graph_move_node":
            {
                var nodeId = payload.TryGetProperty("nodeId", out var np) ? np.GetString() ?? "" : "";
                var x = payload.TryGetProperty("x", out var xp) ? xp.GetSingle() : 0f;
                var y = payload.TryGetProperty("y", out var yp) ? yp.GetSingle() : 0f;

                var res = _controller.MoveNode(nodeId, x, y);
                Reply(window, "graph_move_node_response", res, GraphJsonContext.Default.GraphOperationResultBoolean);
                break;
            }

            case "graph_validate":
            {
                var res = _controller.ValidateGraph();
                Reply(window, "graph_validate_response", res, GraphJsonContext.Default.GraphValidationResult);
                break;
            }

            case "graph_add_variable":
            {
                var name = payload.TryGetProperty("name", out var np) ? np.GetString() ?? "NewVar" : "NewVar";
                var typeStr = payload.TryGetProperty("type", out var tp) ? tp.GetString() ?? "Float" : "Float";
                var defVal = payload.TryGetProperty("defaultValue", out var dp) ? dp.GetString() ?? "0" : "0";
                var isPublic = !payload.TryGetProperty("isPublic", out var ip) || ip.GetBoolean();
                var pinType = Enum.TryParse<GraphPinType>(typeStr, true, out var parsedPinType) ? parsedPinType : GraphPinType.Float;

                var res = _controller.AddVariable(name, pinType, defVal, isPublic);
                Reply(window, "graph_add_variable_response", res, GraphJsonContext.Default.GraphOperationResultGraphVariable);
                break;
            }

            case "graph_remove_variable":
            {
                var varId = payload.TryGetProperty("variableId", out var vp) ? vp.GetString() ?? "" : "";
                var res = _controller.RemoveVariable(varId);
                Reply(window, "graph_remove_variable_response", res, GraphJsonContext.Default.GraphOperationResultBoolean);
                break;
            }

            default:
            {
                window?.SendWebMessage(JsonSerializer.Serialize(new
                {
                    type = "error",
                    message = $"Unknown graph sub-action: '{subAction}'"
                }));
                break;
            }
        }
    }

    private static void Reply<T>(PhotinoWindow? window, string type, T data, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.Serialize(data, typeInfo);
        window?.SendWebMessage($"{{\"type\":\"{type}\",\"action\":\"graph_action\",\"data\":{json}}}");
    }
}
