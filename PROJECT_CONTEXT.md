---
project_name: "Blockout Studio"
current_version: "1.0.0-alpha"
architecture_pattern: "Modular Service-Oriented Desktop Architecture (.NET 10 DI + Photino IPC Router + ES6 Module ECS Frontend + Native C++20 SIMD/Vulkan Compute Core)"
primary_stack: [".NET 10 (C# 13)", "Photino.NET 4.0.16 (WebView2 / WebKitGTK)", "Three.js (WebGL 2.0)", "C++20 (MSVC / GCC AVX2 & AVX-512)", "LunarG Vulkan SDK (SPIR-V / Compute)", "OpenUSD (.usda/.usdc) & Wavefront (.obj)"]
last_updated: 2026-10-18
status: "Active / In-Development"
---

# Project Context & Agent Memory Base

This document serves as the absolute single source of truth (SSOT) for context, state, and foundational rules of this project. Any AI agent, Gemini model, or new chat session MUST read this file entirely before executing commands or generating code to prevent context drift.

---

## 🎯 1. Project Overview & Core Goals
* **System Purpose:** **Blockout Studio** is a high-performance, native-accelerated 3D level blockout, CSG modeling, ECS gameplay prototyping, and multi-platform packaging studio built on .NET 10, C++20 SIMD, Vulkan Compute, and WebGL 2.0. It bridges rapid SketchUp/Hammer-style architectural blockout directly into game engines and DCCs (via native `.obj` and non-destructive `.usda` stages) while also functioning as a standalone game editor with Play-In-Editor (PIE) and native Windows/Linux/Android APK packaging.
* **Target Audience:** Level Designers, Environment Artists, Technical Designers, and Indie/Solo Game Developers targeting Desktop PC and Mobile (Android) platforms.
* **Value Proposition:** Eliminates the heavy iteration loop of blocking out levels inside massive engine editors by providing instant metric-accurate (`1 unit = 1 cm`) SketchUp-style push/pull and CSG boolean modeling, hybrid Unity `.meta` + binary `AssetRegistry.bin` asset management, GPU/SIMD-accelerated Ambient Occlusion baking, instant in-editor first-person playtesting (PIE) with standard human dummy metrics (`180 cm` height), and direct 1-click export to standalone compiled builds or industry-standard 3D formats (OBJ / USD).

---

## 🛠️ 2. Technical Stack & Environment
* **Backend:**
  * **Language & Runtime:** C# 13 on **.NET 10.0** (`net10.0`, `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`, `<Nullable>enable</Nullable>`).
  * **Desktop Shell:** `Photino.NET` (`4.0.16`) hosting native OS WebView2 (Windows) / WebKitGTK (Linux).
  * **Dependency Injection:** `Microsoft.Extensions.DependencyInjection` (`10.0.13`) configured in `Services/ServiceRegistration.cs`.
  * **Clipboard Integration:** `TextCopy` (`6.2.1`) for direct OS clipboard export of level geometry and actors.
  * **Native C++20 Core (`EngineNative.dll` / `libEngineNative.so`):** Custom C++20 shared library (`Native/EngineNative/`) compiled with `/std:c++20 /O2 /arch:AVX2` and bound via `.NET 10` source-generated `[LibraryImport]` (`Services/NativeInterop/EngineNativeBindings.cs`) for hardware-intrinsic AVX2/AVX-512 ray-triangle intersection, CSG math, and Vertex AO baking.
* **Frontend:**
  * **Architecture:** Native ES6 Modules (`wwwroot/js/*.js`) with zero bundler build-step latency.
  * **3D Rendering Engine:** `Three.js` (WebGL 2.0) with custom 4-tier Mobile-to-Console Scalability (`Low`, `Med`, `High`, `Epic`), `THREE.InstancedMesh` batching, and `THREE.LOD` auto-switching.
  * **UI & Styling:** Vanilla HTML5 & Modern CSS (`wwwroot/index.html`, `wwwroot/css/main.css`) using the **HTML Popover API** (`popover` / `popovertarget`), **CSS Anchor Positioning** (`anchor-name`, `position-anchor`, `position-area`), **CSS Grid** (`grid-template-areas`), and responsive Flexbox (`flex-wrap: balance`, `align-items: safe center`).
  * **State Management:** Event-driven IPC synchronization over `ipcBridge.js`, Undo/Redo command snapshot stack in `historyManager.js`, and Entity-Component-System (ECS) state inside `sceneManager.js` and `pieManager.js`.
* **Database, Storage & Binary Formats:**
  * **Hybrid Asset Database (`Services/Assets/AssetDatabaseService.cs`):** Combines Unity-style YAML/JSON `.meta` sidecar files (persistent 32-char hex GUIDs) with a fast binary/JSON cache at `<ProjectRoot>/Library/AssetRegistry.bin`.
  * **Binary Geometry Artifact Cache (`.beng`):** Zero-stall compiled mesh cache in `<ProjectRoot>/Library/ArtifactCache/<guid>.beng` with magic header `BENG` (`0x474E4542`), storing flat `Float32Array` (Positions, Normals, UVs, Colors) and `Uint32Array` (Indices).
  * **Scene & Prefab Composition (`.usda` / `.obj`):** Native zero-allocation `Span<T>` Wavefront `.obj` processor (`ObjNativeProcessor.cs`) and OpenUSD `.usda` stage composer (`UsdStageService.cs`) supporting `subLayers`, `references`, `payload`, `variantSets`, and non-destructive `over` delta opinions.
* **External Integrations & Native SDKs:**
  * **LunarG Vulkan SDK (`VULKAN_SDK`):** `glslc`, `glslangValidator`, `spirv-opt`, `spirv-cross`, `vulkaninfo`, `vulkan-1.lib` (`Services/Graphics/VulkanComputeService.cs` & `Services/BuildPipeline/SdkDetectorService.cs`).
  * **Windows 11 SDK & MSVC Toolchain:** Discovered via registry (`KitsRoot10`) and `vswhere.exe` to drive `vcvars64.bat`, `cl.exe`, `link.exe`, `rc.exe`, and `signtool.exe` (`Services/BuildPipeline/NativeCompilerService.cs`).
  * **Android SDK, NDK & Android CLI:** Supports custom paths in `ProjectSettings/SDKPaths.json` and `ANDROID_HOME`; integrates `adb`, `aapt2`, `apksigner`, NDK `clang++`, and `android.exe` CLI for APK packaging and 1-click USB/wireless deployment (`Services/BuildPipeline/PlatformBuilderService.cs`).
  * **Linux Toolchain:** Native or WSL2 `gcc`/`g++`, `make`, `ninja`, and `pkg-config`.

---

## 📐 3. Architecture & Code Conventions
Agents must strictly follow these rules to maintain codebase integrity:

* **Directory Structure Rules:**
  * `Program.cs`: Application entry point, CLI test runner flag (`--test`), `VulkanAngleConfigurator` startup, and `PhotinoWindow` initialization (including `.SetMinSize(1024, 640)`).
  * `Services/ServiceRegistration.cs`: Central DI container registration. Every new service and `IIpcActionHandler` MUST be registered here.
  * `Services/Ipc/`: Contains `IIpcRouter.cs`, `IpcRouter.cs`, `IIpcActionHandler.cs`, and `Services/Ipc/Handlers/*.cs` (one handler class per IPC action family).
  * `Services/Assets/`: Project workspace scaffolding, `.meta` sidecars, `AssetRegistry.bin`, and asset import pipelines.
  * `Services/Geometry/`: `CsgEngine.cs`, `ObjNativeProcessor.cs`, `ObjUsdPipelineService.cs`, `UsdStageService.cs`, and `LodGeneratorService.cs`.
  * `Services/Graphics/`: `VulkanComputeService.cs`, `VertexAoBaker.cs`, and `TextureBaker.cs`.
  * `Services/BuildPipeline/`: `SdkDetectorService.cs`, `NativeCompilerService.cs`, and `PlatformBuilderService.cs`.
  * `Services/NativeInterop/`: C# `[LibraryImport]` P/Invoke bindings to `EngineNative.dll`.
  * `Services/AutomatedTests.cs`: Headless end-to-end automated test suite (`dotnet run -- --test`). Every major feature must add a corresponding test here.
  * `wwwroot/js/`: Frontend ES6 modules (`ipcBridge.js`, `engine.js`, `transformGizmo.js`, `sceneManager.js`, `assetManager.js`, `usdObjLoader.js`, `pieManager.js`, `buildManager.js`, `app.js`), tools in `wwwroot/js/tools/`, and ECS runtime in `wwwroot/js/ecs/`.
* **Naming Conventions:**
  * **C# Backend:** `PascalCase` for classes, interfaces (`IServiceName`), methods, properties, and records; `_camelCase` for private readonly fields; `snake_case` for IPC `Action` strings (e.g., `"bake_ao"`, `"compile_native_module"`).
  * **JavaScript Frontend:** `camelCase` for functions, variables, and file names (`sceneManager.js`, `pieManager.js`); `PascalCase` for ECS classes (`Entity`, `Component`); `UPPER_SNAKE_CASE` for constants (`SCALABILITY_TIERS`).
  * **CSS:** `kebab-case` for IDs and class names (`#left-sidebar`, `.menubar-popover`, `.tool-grid`).
* **State Management:**
  * All frontend-to-backend communication flows asynchronously through `ipcBridge.js` (`sendToBackend({ action, ...payload })`) $\rightarrow$ Photino `WebMessageReceived` $\rightarrow$ `IpcRouter.DispatchAsync` $\rightarrow$ matching `IIpcActionHandler.HandleAsync` $\rightarrow$ `window.SendWebMessage(jsonResponse)` $\rightarrow$ frontend `registerHandler(responseAction, callback)` and `window.dispatchEvent(new CustomEvent('ipc-message', { detail }))`.
* **Error Handling:**
  * `IpcRouter.DispatchAsync` wraps all handler executions in a structured `try/catch` that logs diagnostics to `Console.Error` and returns `{ action = "error", message = ex.Message }` to the frontend toast system (`showToast(msg, true)`).
  * External CLI processes (`glslc`, `cl.exe`, `adb`, `wsl`) must always use timeout-guarded asynchronous `Process` execution and never throw unhandled exceptions if an external SDK is not installed on the host.

---

## 🚦 4. Critical Business Rules (The "Never-Break" Rules)
* **Rule #1 (Spatial & Metric Standards):** `1 Engine Unit = 1 cm` (`100 units = 1 m`). The editor uses a Z-Up coordinate convention. All architectural presets and the `CharacterControllerComponent` must honor standard human reference metrics (`180 cm` height, `60 cm` width, `45 cm` max step height).
* **Rule #2 (Non-Destructive USD & Asset Pipeline):** Transforming an imported `.obj`/`.usd` asset or overriding its material in a level must NEVER destructively overwrite the source asset in `Assets/Meshes/`. Instead, write non-destructive USD `over` opinions via `UsdStageService.cs` and preserve `.meta` GUIDs.
* **Rule #3 (Hardware & Hybrid-GPU Resilience):**
  * On Windows 11 hybrid-GPU (Optimus) laptops, Chromium WebView2 crashes its GPU process when forced onto experimental Vulkan ANGLE (`--use-angle=vulkan`). Therefore, `VulkanAngleConfigurator.cs` MUST default Windows WebView2 to `--use-angle=d3d11` (unless `FORCE_VULKAN_ANGLE=1` or `--vulkan-angle` is passed), while C# backend compute (`VulkanComputeService.cs`) uses the native LunarG Vulkan SDK directly.
  * Every compute-heavy operation (`VertexAoBaker`, `CsgEngine`, `TextureBaker`) must maintain a 3-tier fallback chain: **Vulkan GPU Compute $\rightarrow$ Native C++20 AVX2/AVX-512 (`EngineNative.dll`) $\rightarrow$ Managed C# `.NET 10` `Parallel.For` + `System.Numerics.Vector<T>`**.
* **Rule #4 (No Non-Module Three.js Imports & Strict Script Order):** `wwwroot/lib/three/three.min.js` is loaded globally in `<head>`. Frontend ES modules must NEVER write `import * as THREE from '../lib/three/three.min.js'` (which shadows `window.THREE` with an empty object `{}`). Furthermore, `<script type="module">` tags in `wwwroot/index.html` must maintain topological order (`ipcBridge.js` $\rightarrow$ `engine.js` $\rightarrow$ `transformGizmo.js` $\rightarrow$ `sceneManager.js` $\rightarrow$ `assetManager.js` $\rightarrow$ `usdObjLoader.js` $\rightarrow$ `pieManager.js` $\rightarrow$ `buildManager.js` $\rightarrow$ `app.js`).
* **Rule #5 (Mandatory Verification Gate):** Before completing any code modification, all 28 automated backend/pipeline tests (`dotnet run -- --test`) and `node -c` syntax checks across all JS files must pass with zero errors.

---

## 📝 5. Architectural Decision Log (ADR) & Conversation Memory
*Every time a major design choice is made or a session ends, the model must append a new entry below.*

### [2026-10-14] - Session #1: Core Engine Architecture & Module 1 (Hybrid Asset Database)
* **Context:** Initialized the .NET 10 + Photino.NET + Three.js hybrid desktop studio with DI (`ServiceRegistration.cs`), `IpcRouter`, Native C++20 `EngineNative.dll` (`[LibraryImport]`), and Module 1 (Unity `.meta` sidecars + `AssetRegistry.bin` + Bottom Content Drawer UI).
* **Decisions Made:** Chose deterministic `.meta` sidecars paired with a fast `Library/AssetRegistry.bin` cache so assets can be moved/renamed without breaking scene references.

### [2026-10-15] - Session #2: Modules 2, 3, 4 & 5 (Geometry, Vulkan/Scalability, ECS/PIE, Build/SDK Pipeline)
* **Context:** Implemented Module 2 (`.obj` `Span<T>` parser, `.usda` stage service with `over` opinions, `.beng` binary mesh cache), Module 3 (LunarG Vulkan SDK `glslc`/`spirv-opt`/`spirv-cross` shader pipeline, 4-tier Mobile-to-Console scalability switcher, Auto-LOD generator), Module 4 (ECS entity/component architecture + Play-In-Editor first-person runtime), and Module 5 (`SdkDetectorService`, `NativeCompilerService` for MSVC `cl.exe` / Linux `g++`, `PlatformBuilderService` for Windows/Linux/Android APK + ADB deployment).
* **Decisions Made:** Dual-registered all 9 core services in `ServiceRegistration.cs` (both `IService` and concrete `Service` singletons) and wired all 32 IPC handlers into `IpcRouter`.

### [2026-10-16] - Session #3: Hybrid-GPU WebView2 Fix, Top Menu Bar, Left Tools Panel & Documentation Suite
* **Context:** Resolved a white-screen WebGL crash on hybrid Optimus GPUs, refactored the Editor UI to add a native Top Menu Bar (`File`, `Edit`, `Assets`, `Tools`, `Build`, `View`) and Left Tools Sidebar (`#left-sidebar`), enforced a `1024x640` minimum window size with responsive toolbar wrapping, and authored the complete project documentation suite (`PROJECT_CONTEXT.md`, `docs/USER_GUIDE.md`, `docs/TECHNICAL_REFERENCE.md`, `README.md`).
* **Decisions Made:** Defaulted Windows WebView2 to `--use-angle=d3d11` in `VulkanAngleConfigurator.cs` for rock-solid swapchain stability while preserving native Vulkan compute in C#. Built the Top Menu Bar using native HTML Popover API + CSS Anchor Positioning.

### [2026-10-20] - Session #4: UE5-Inspired Blueprint Visual Scripting Engine (C# 13 + Roslyn)
* **Context:** Implemented an authentic Unreal Engine 5-inspired Blueprint Visual Scripting Studio completely running in C# (.NET 10 / C# 13) without interacting with Unreal Engine itself.
* **Decisions Made:**
  - **Roslyn Transpilation & In-Memory Compilation:** Added `Microsoft.CodeAnalysis.CSharp` 5.9.0. Visual node graphs transpile into strongly-typed C# 13 classes (`ActorBlueprint` / `LevelBlueprint`) and compile dynamically with Roslyn into collectible `AssemblyLoadContext` instances.
  - **Storage & Assets:** Graphs saved as `.bprint` JSON in `Assets/Blueprints/` with Unity-style `.meta` GUID sidecars; transpiled readable C# code automatically written to `Assets/Scripts/Generated/{Name}.cs`.
  - **Backend Simulation Runtime:** Built `BlueprintSimulationService` managing active actor instances (`OnBeginPlay`, `OnTick`, `OnCollisionEnter`) during Play-In-Editor (PIE), streaming transform changes to Three.js over IPC.
  - **Frontend UI & Canvas:** Integrated a top viewport tab switcher (`3D Viewport` vs `Blueprint Editor`) with authentic UE5 aesthetic (dark carbon grid, SVG cubic bezier wires with exec pulse animation, UE5 pin color-coding, right-click searchable action menu, inline value inputs, and a live C# code preview split-pane).
  - **Automated Verification:** Added Test 26 to `AutomatedTests.cs`, validating serialization, transpilation, Roslyn compilation, and simulation step calculations. All 26 automated tests pass with 0 errors.

### [2026-10-21] - Session #5: Project Save & Load Hardening + Laptop Touchpad & Trackpad Gestures
* **Context:** Validated and hardened the project file persistence pipeline and added full laptop touchpad/trackpad gesture navigation for mobile workflows.
* **Decisions Made:**
  - **IPC Normalization & Save Project Handler:** Fixed parameter normalization in `ipcBridge.js` so both `sendToBackend(action, payload)` and `sendToBackend({ action, ...payload })` format correctly without nested action object bugs. Upgraded `SaveProjectHandler.cs` to safely accept both `JsonValueKind.String` and `JsonValueKind.Object`.
  - **Comprehensive Scene Serialization (`formatVersion: 2`):** Unified `sceneBridge.js` save/load with `historyManager.js` (`serializeScene` and `deserializeScene`), preserving actor meshes, transforms, lights, CSG holes, vertex AO colors, and ECS components (`BlueprintComponent`, `ColliderComponent`, `CharacterControllerComponent`, `AudioSourceComponent`). Added schema versioning and legacy block fallback.
  - **Touchpad & Trackpad Gesture Engine:**
    - Per Modern Web Guidance, applied `touch-action: none;`, `overscroll-behavior: none;`, and `scroll-axis-lock: none;` to `#viewport-container`, `#blueprint-canvas`, and canvas containers.
    - Intercepted `ctrlKey + wheel` gestures to prevent Chromium/WebView2 whole-page UI zoom while executing smooth viewport & canvas zooming.
    - Implemented two-finger 2D camera panning (`e.deltaX`, `e.deltaY` with `deltaMode === 0`) in screen-space coordinates.
    - Added `Alt + Pointer Drag` (Blender / Maya / Unity standard) to allow laptop users without a 3-button mouse to orbit the 3D camera.
    - Implemented two-finger canvas panning and pinch-to-zoom centered on the mouse cursor in `blueprintCanvas.js`, plus `Alt`/`Space` drag navigation.
  - **Verification:** Added Test 27 to `AutomatedTests.cs` testing full scene roundtrips and legacy `.pjblock` format ingestion. All 27 automated tests pass with 0 errors and all JS modules pass `node -c`.

### [2026-10-21] - Session #6: Official OpenUSD Format Validation & Schema Hardening
* **Context:** Reviewed official OpenUSD 24.x/26.x specifications (https://openusd.org/release/index.html) and validated generated USDA files against the official Pixar OpenUSD runtime (`pxr.Usd` and `pxr.UsdUtils.ComplianceChecker`).
* **Decisions Made:**
  - **Mesh Extents & Schema Attributes:** Updated `UsdPipeline.cs` to calculate and emit `float3[] extent = [(minX, minY, minZ), (maxX, maxY, maxZ)]` for `UsdGeomBoundable` compliance, formatted texture coordinates with `texCoord2f[] primvars:st`, and emitted standard `normal3f[] normals`.
  - **MaterialBindingAPI Applied Schema:** In `UsdStageService.cs`, updated prim headers authoring material bindings to declare `prepend apiSchemas = ["MaterialBindingAPI"]` with proper USDA newline-delimited metadata dictionary syntax (preventing syntax errors caused by comma delimiters in USDA metadata).
  - **Non-Destructive Over Opinion Semantics:** Corrected sublayer composition rules so that `over` opinions target entities defined in sublayers without creating duplicate sibling prim definitions in the same layer.
  - **Automated Validation:** Generated 3 sample USDA files (`sample_blockout_mesh.usda`, `sample_lighting_rig.usda`, `sample_composition_stage.usda`) and validated them via Blender 5.2's official Pixar `pxr` runtime, passing with 0 errors, 0 warnings, and 0 failed compliance checks. Added Test 28 to `AutomatedTests.cs`. All 28 automated tests pass with 0 errors.

---

## 🔄 6. Handover Instructions for Future Models
*When closing this chat session, copy and paste the current state here:*

* **Current Focus:** Completed OpenUSD specification review and validation against official Pixar `pxr` runtime (`pxr.Usd` and `pxr.UsdUtils.ComplianceChecker`). All 28 automated tests pass.
* **Blocking Issues:** None. All 28 automated tests (`dotnet run -- --test`) pass and all JS modules pass `node -c` syntax validation.
* **Next Immediate Step:** Ready for next user instruction.

