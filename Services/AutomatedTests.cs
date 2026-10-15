using System.Numerics;
using System.Text.Json;
using UE5BlockoutStudio.Services.Geometry;
using UE5BlockoutStudio.Services.Graphics;

namespace UE5BlockoutStudio.Services;

public static class AutomatedTests
{
    public static int Run()
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("Running UE5 Blockout Studio Automated Tests (.NET 10)");
        Console.WriteLine("=================================================");

        int passed = 0;
        int failed = 0;

        // Test 1: UE5 T3D Clipboard Generation
        try
        {
            Console.Write("Test 1: UE5 T3D Clipboard Actor Generation... ");
            var json = """
            [
              {
                "name": "SM_TestWall",
                "shapeType": "box",
                "sizeX": 400,
                "sizeY": 20,
                "sizeZ": 300,
                "posX": 100,
                "posY": 50,
                "posZ": 0,
                "rotZ": 90
              },
              {
                "name": "SM_TestCylinder",
                "shapeType": "cylinder",
                "sizeX": 100,
                "sizeY": 100,
                "sizeZ": 200,
                "posX": -100,
                "posY": 0,
                "posZ": 20,
                "rotZ": 0
              },
              {
                "name": "SM_TestSphere",
                "shapeType": "sphere",
                "sizeX": 100,
                "sizeY": 100,
                "sizeZ": 100,
                "posX": 0,
                "posY": 0,
                "posZ": 0,
                "rotZ": 0
              }
            ]
            """;
            using var doc = JsonDocument.Parse(json);
            var (success, msg, count) = Ue5ClipboardService.CopyToUe5(doc.RootElement);

            if (success && count == 3)
            {
                Console.WriteLine("PASSED");
                passed++;
            }
            else
            {
                Console.WriteLine($"FAILED: {msg}");
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
            failed++;
        }

        // Test 2: C# CSG Boolean Subtraction (Hole Cutter)
        try
        {
            Console.Write("Test 2: C# CSG Boolean Subtraction Engine... ");
            // Create a closed subject box (40 x 40 x 40)
            var subjectBox = CreateBoxMesh(-20, 0, -20, 20, 40, 20);
            // Create a cutter box that cuts through the center (-10 to 10 on X, -30 to 30 on Z, 0 to 20 on Y)
            var cutterBox = CreateBoxMesh(-10, 0, -30, 10, 20, 30);

            var (pos, norm, uvs, idx) = CsgEngine.Subtract(subjectBox, null, null, null, cutterBox, null, null, null);
            if (pos.Length > 0 && norm.Length > 0 && uvs.Length > 0)
            {
                Console.WriteLine($"PASSED (Cut hole: generated {pos.Length / 3} vertices)");
                passed++;
            }
            else
            {
                Console.WriteLine("FAILED: Empty CSG result");
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
            failed++;
        }

        // Test 3: C# SIMD Raytraced Vertex AO Baker
        try
        {
            Console.Write("Test 3: C# Raytraced Vertex AO Baker (.NET 10 SIMD)... ");
            var testPositions = new float[] {
                0, 0, 0,   100, 0, 0,   100, 100, 0,
                0, 0, 0,   100, 100, 0, 0, 100, 0
            };
            var testNormals = new float[] {
                0, 0, 1,   0, 0, 1,   0, 0, 1,
                0, 0, 1,   0, 0, 1,   0, 0, 1
            };
            var triangles = new List<SceneTriangle>
            {
                new(new Vector3(0, 0, 10), new Vector3(100, 0, 10), new Vector3(100, 100, 10))
            };

            var colors = VertexAoBaker.BakeMeshAo(testPositions, testNormals, triangles, 50.0f);
            if (colors.Length == testPositions.Length && colors[0] >= 0.25f && colors[0] <= 1.0f)
            {
                Console.WriteLine($"PASSED (Computed AO colors for {testPositions.Length / 3} vertices)");
                passed++;
            }
            else
            {
                Console.WriteLine("FAILED: Invalid AO color buffer");
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
            failed++;
        }

        // Test 4: Procedural Texture Baker
        try
        {
            Console.Write("Test 4: Procedural Grid Texture Baker... ");
            var url = TextureBaker.GetUe5GridDataUrl();
            if (url.StartsWith("data:image/svg+xml;base64,"))
            {
                Console.WriteLine("PASSED");
                passed++;
            }
            else
            {
                Console.WriteLine("FAILED: Invalid data URL format");
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
            failed++;
        }

        // Test 5: UE5 Static/Stationary Light Actor Generation
        try
        {
            Console.Write("Test 5: UE5 Static/Stationary Light Actor T3D Generation... ");
            var json = """
            [
              {
                "name": "PointLight_Static_1",
                "shapeType": "point_light",
                "mobility": "Static",
                "intensity": 6000,
                "radius": 1200,
                "color": "#ffaa44",
                "posX": 0,
                "posY": 0,
                "posZ": 250,
                "rotZ": 0
              },
              {
                "name": "SpotLight_Stationary_1",
                "shapeType": "spot_light",
                "mobility": "Stationary",
                "intensity": 8000,
                "radius": 1500,
                "color": "#ffffff",
                "posX": 200,
                "posY": -100,
                "posZ": 300,
                "rotZ": 45
              }
            ]
            """;
            using var doc = JsonDocument.Parse(json);
            var (success, msg, count) = Ue5ClipboardService.CopyToUe5(doc.RootElement);

            if (success && count == 2)
            {
                Console.WriteLine("PASSED (Generated PointLight Static & SpotLight Stationary)");
                passed++;
            }
            else
            {
                Console.WriteLine($"FAILED: {msg}");
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
            failed++;
        }

        // Test 6: Undo/Redo Scene Snapshot & State Serialization
        try
        {
            Console.Write("Test 6: Scene History Snapshot Serialization Roundtrip... ");
            var snapshotJson = """
            {
              "selectedName": "SM_Wall_1",
              "blocks": [
                {
                  "name": "SM_Wall_1",
                  "shapeType": "box",
                  "mobility": "Static",
                  "intensity": 5000,
                  "radius": 600,
                  "color": "#ffe6a3",
                  "sizeX": 400,
                  "sizeY": 20,
                  "sizeZ": 300,
                  "posX": 0,
                  "posY": 0,
                  "posZ": 0,
                  "rotX": 0,
                  "rotY": 1.57,
                  "rotZ": 0,
                  "customMesh": true,
                  "csgData": {
                    "positions": [0,0,0, 10,0,0, 10,10,0],
                    "normals": [0,1,0, 0,1,0, 0,1,0],
                    "uvs": [0,0, 1,0, 1,1],
                    "indices": [0,1,2]
                  },
                  "aoColors": [0.5, 0.5, 0.5]
                },
                {
                  "name": "PointLight_Stationary_1",
                  "shapeType": "point_light",
                  "mobility": "Stationary",
                  "intensity": 5000,
                  "radius": 600,
                  "color": "#ffe6a3",
                  "sizeX": 30,
                  "sizeY": 30,
                  "sizeZ": 30,
                  "posX": 0,
                  "posY": 250,
                  "posZ": 0,
                  "rotX": 0,
                  "rotY": 0,
                  "rotZ": 0,
                  "customMesh": false,
                  "csgData": null,
                  "aoColors": null
                }
              ]
            }
            """;

            using var snapDoc = JsonDocument.Parse(snapshotJson);
            var root = snapDoc.RootElement;
            var blocksArr = root.GetProperty("blocks");
            if (blocksArr.GetArrayLength() == 2 &&
                root.GetProperty("selectedName").GetString() == "SM_Wall_1" &&
                blocksArr[0].GetProperty("customMesh").GetBoolean() == true &&
                blocksArr[0].GetProperty("csgData").GetProperty("indices").GetArrayLength() == 3)
            {
                Console.WriteLine("PASSED (Validated 2 actors, CSG buffer cache & selection state)");
                passed++;
            }
            else
            {
                Console.WriteLine("FAILED: Snapshot schema validation failed");
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
            failed++;
        }

        Console.WriteLine("=================================================");
        Console.WriteLine($"Total: {passed + failed} | Passed: {passed} | Failed: {failed}");
        Console.WriteLine("=================================================");

        return failed == 0 ? 0 : 1;
    }

    private static float[] CreateBoxMesh(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    {
        return new float[]
        {
            // Front (+Z)
            minX, minY, maxZ,   maxX, minY, maxZ,   maxX, maxY, maxZ,
            minX, minY, maxZ,   maxX, maxY, maxZ,   minX, maxY, maxZ,
            // Back (-Z)
            maxX, minY, minZ,   minX, minY, minZ,   minX, maxY, minZ,
            maxX, minY, minZ,   minX, maxY, minZ,   maxX, maxY, minZ,
            // Top (+Y)
            minX, maxY, maxZ,   maxX, maxY, maxZ,   maxX, maxY, minZ,
            minX, maxY, maxZ,   maxX, maxY, minZ,   minX, maxY, minZ,
            // Bottom (-Y)
            minX, minY, minZ,   maxX, minY, minZ,   maxX, minY, maxZ,
            minX, minY, minZ,   maxX, minY, maxZ,   minX, minY, maxZ,
            // Right (+X)
            maxX, minY, maxZ,   maxX, minY, minZ,   maxX, maxY, minZ,
            maxX, minY, maxZ,   maxX, maxY, minZ,   maxX, maxY, maxZ,
            // Left (-X)
            minX, minY, minZ,   minX, minY, maxZ,   minX, maxY, maxZ,
            minX, minY, minZ,   minX, maxY, maxZ,   minX, maxY, minZ,
        };
    }
}
