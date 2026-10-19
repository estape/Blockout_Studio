# Blockout Studio — User Guide & Manual

Welcome to **Blockout Studio**, a fast, metric-accurate 3D architectural blockout, CSG modeling, ECS gameplay prototyping, and multi-platform packaging studio engineered for Desktop PC, Mobile (Android), and 3D game development workflows.

---

## Table of Contents
1. [Quick Start & Launching](#1-quick-start--launching)
2. [Spatial Standards & Metric Scale](#2-spatial-standards--metric-scale)
3. [Interface Overview](#3-interface-overview)
   - [Top Menu Bar (`File`, `Edit`, `Assets`, `Tools`, `Build`, `View`)](#31-top-menu-bar)
   - [Top Toolbar](#32-top-toolbar)
   - [Left Tools Sidebar (`#left-sidebar`)](#33-left-tools-sidebar)
   - [Right Inspector & Forms Sidebar (`#sidebar`)](#34-right-inspector--forms-sidebar)
   - [Bottom Content Drawer & Asset Manager](#35-bottom-content-drawer--asset-manager)
4. [Keyboard & Mouse Shortcuts Reference](#4-keyboard--mouse-shortcuts-reference)
5. [Core Workflows (Step-by-Step)](#5-core-workflows-step-by-step)
   - [Workflow 1: Sketching, Extruding & Cutting Holes (CSG)](#workflow-1-sketching-extruding--cutting-holes-csg)
   - [Workflow 2: Exporting Levels (Clipboard / OBJ / USD)](#workflow-2-exporting-levels-clipboard--obj--usd)
   - [Workflow 3: Managing Projects, `.OBJ`/`.USD` Assets & Reference Graphs](#workflow-3-managing-projects-objusd-assets--reference-graphs)
   - [Workflow 4: ECS Gameplay Components & Play-In-Editor (PIE)](#workflow-4-ecs-gameplay-components--play-in-editor-pie)
   - [Workflow 5: Baking Ambient Occlusion & 4-Tier Scalability](#workflow-5-baking-ambient-occlusion--4-tier-scalability)
   - [Workflow 6: Compiling C++20 Plugins, Packaging Builds & ADB Deployment](#workflow-6-compiling-c20-plugins-packaging-builds--adb-deployment)

---

## 1. Quick Start & Launching

### Prerequisites
- **.NET 10.0 SDK** installed on Windows 10/11 or Linux.
- *(Optional)* **LunarG Vulkan SDK** (`VULKAN_SDK`) for SPIR-V shader compilation and GPU-accelerated Ambient Occlusion baking.
- *(Optional)* **Visual Studio 2022 / MSVC Build Tools** (Windows) or **GCC/G++** (Linux) for compiling custom C++20 native plugins.
- *(Optional)* **Android SDK & NDK** for packaging `.apk` builds and deploying directly to connected Android devices over `adb`.

### Launching the Editor
Open a terminal in the project folder (`D:\Rodrigo\Documents\Dev\vscode\Desktop\dotNET\Blockout_Studio`) and run:

```powershell
dotnet run
```

### Running the Automated Diagnostics & Test Suite
To verify all 25 backend, geometry, shader, ECS, and compiler subsystems headlessly:

```powershell
dotnet run -- --test
```

---

## 2. Spatial Standards & Scale Metrics

Blockout Studio is locked to **standard spatial conventions** so your blockouts never require rescaling after export:
- **Unit Scale:** `1 Unit = 1 cm` (`100 Units = 1 Meter`).
- **Coordinate System:** **Z-Up** (`X` = Forward/Right, `Y` = Depth, `Z` = Elevation/Height).
- **Pivot Convention:** Bottom-center (`Z = 0` at the base of the object) so walls, floors, ramps, and pillars snap flush onto floors.
- **180cm Dummy Reference (`180 cm`):** A yellow wireframe/semi-transparent reference silhouette (`180 cm` tall, `60 cm` wide) stands in the scene to give instant visual feedback on door heights, cover height, and stair proportions.
  - Standard Door Height: `210 cm – 240 cm`
  - Standard Wall Height: `300 cm – 400 cm`
  - Standard Crouch Cover Height: `90 cm – 110 cm`
  - Max Step Height: `45 cm`

---

## 3. Interface Overview

```
+-----------------------------------------------------------------------------------+
| [B] Blockout Studio       File   Edit   Assets   Tools   Build   View             | <- Top Menu Bar
+-----------------------------------------------------------------------------------+
| Snap:[10cm] | Undo Redo | [Play][Pause][Stop] | Quality:[Low][Med][High][Epic] ...| <- Top Toolbar
+---------------+---------------------------------------------------+---------------+
| TOOLS         |                                                   | FORMS         |
| [Move] [Rot]  |                                                   | [Wall][Floor] |
| [Rect] [Circ] |                                                   | [Ramp][Cylin] |
| [Push] [Hole] |                 3D VIEWPORT                       | [Stair][Arch] |
| [Sphe] [Lght] |           (1 Unit = 1 cm, Z-Up)                   +---------------+
|               |                                                   | SELECTED OBJ  |
+---------------+                                                   | Name, Color   |
| HOLE CUTTER   |                                                   | W / L / H (cm)|
| Shape/Depth   |  [HUD Overlay]                                    | X / Y / Z (cm)|
| [Punch Hole]  |  [Content Drawer (Ctrl+Space)]                    +---------------+
|               +---------------------------------------------------+ ECS / OUTLINER|
+---------------+---------------------------------------------------+---------------+
```

> [!NOTE]
> **Responsive Window Layout:** The application enforces a minimum window size of `1024 × 640` so the 3D viewport is never crushed. When resized smaller, the Top Toolbar automatically wraps onto balanced rows without hiding or clipping any buttons, and both left and right sidebars scroll independently.

### 3.1 Top Menu Bar
Located at the very top of the window:

- **File Menu**:
  - **New Project (`Ctrl+N`)**: Scaffolds a complete project directory structure (`Assets/`, `Library/`, `ProjectSettings/`, `Saved/`, `Binaries/`) at a chosen disk path.
  - **Open Project (`Ctrl+Shift+O`)**: Opens an existing Blockout Studio project directory and indexes its Asset Registry.
  - **Save Project (.json) (`Ctrl+S`)**: Saves the current level actors, CSG geometry buffers, lights, and ECS components to a `.json` project file.
  - **Load Project (.json) (`Ctrl+O`)**: Restores a saved `.json` project file.
  - **Import Asset...**: Imports external `.obj`, `.usda`/`.usd`, textures, audio, or script files into the active project's `Assets/` folder and generates a `.meta` sidecar.
  - **Save Selected as .OBJ**: Converts the currently selected blockout/CSG mesh into a reusable `.obj` asset inside `Assets/Meshes/` with baked Vertex AO colors (`v x y z r g b`).
  - **Save Level as .USD Stage**: Saves the entire scene graph as a non-destructive `.usda` composition inside `Assets/Levels/`.
  - **Export Level (.OBJ)** / **Export Level (.USD)**: Exports the level to an external folder for direct import into game engines, DCCs, Blender, or Maya.
- **Edit Menu**:
  - **Undo (`Ctrl+Z`)** & **Redo (`Ctrl+Y`)**: Steps backward or forward through the 50-state scene snapshot history.
  - **Duplicate Selected (`Ctrl+D`)**: Clones the selected actor (offset by `+50 cm` on X and Y).
  - **Delete Selected (`Del`)**: Removes the selected actor or light from the scene.
  - **Copy Level to Clipboard (`Ctrl+C`)**: Generates 3D actor text and copies it to your OS clipboard.
- **Assets Menu**:
  - **Toggle Content Drawer (`Ctrl+Space`)**: Opens or closes the bottom asset browser.
  - **Import Asset to Project...**: Opens the native OS file picker to import assets.
  - **Reference Viewer (Selected Asset)**: Opens the interactive node graph showing upstream/downstream dependencies for the selected asset.
- **Tools Menu**:
  - Quick access to all 8 tools (**Move**, **Rotate**, **Draw Rectangle**, **Draw Circle**, **Push/Pull**, **Hole Cutter**, **Spawn Sphere**, **Spawn Point Light**) and **Bake Ambient Occlusion**.
- **Build Menu**:
  - **Build, Compilers & SDK Settings (`Ctrl+B` / `F7`)**: Opens the 3-tab Native SDK, C++20 Compiler, and Platform Packaging modal.
  - **Compile Native C++20 Plugin**: Immediately compiles `.cpp` files in `Assets/NativePlugins/` using MSVC `cl.exe` or Linux `g++`.
  - **Vulkan / C++ Toolchains Report**: Displays detailed diagnostic status of your host OS, WebGL ANGLE backend, `EngineNative.dll` SIMD capabilities, Vulkan SDK, MSVC, Android SDK, and GCC.
- **View Menu**:
  - **Toggle Scale Dummy (180cm)**: Shows or hides the `180 cm` reference human mesh.
  - **Toggle GPU & Vulkan Performance HUD**: Shows or hides the real-time frame-time, FPS, draw-call, triangle, and active scalability overlay in the top-right of the 3D viewport.
  - **Scalability Tiers (`0`–`3`)**: Switches between **Tier 0 (Mobile Low)**, **Tier 1 (Mobile High)**, **Tier 2 (Desktop PC)**, and **Tier 3 (Console / Epic)**.

---

### 3.2 Top Toolbar
Provides 1-click access to high-frequency runtime and rendering controls:
- **Snap Grid Selector**: Choose grid snapping increments of **Off**, **1 cm**, **5 cm**, **10 cm (Default)**, **25 cm**, **50 cm**, or **100 cm (1m)**.
- **Undo / Redo**: Instant history navigation.
- **Play-In-Editor (PIE) Controls**:
  - **▶ Play**: Snapshots the editor scene, hides gizmos, possesses the `CharacterControllerComponent` (or spawns a default `180 cm` first-person player), locks the mouse cursor, and starts physics, scripts, and 3D spatial audio.
  - **⏸ Pause**: Freezes physics and script updates while keeping the runtime state inspectable.
  - **⏹ Stop**: Exits pointer lock and restores the exact pre-Play editor snapshot.
- **Scalability Bar (`Low`, `Med`, `High`, `Epic` + `GPU HUD`)**: Real-time rendering tier switcher and telemetry overlay toggle.
- **Bake AO**: Runs ray-marched Vertex Ambient Occlusion across all meshes in the scene using Vulkan GPU Compute or SIMD multi-core fallback.
- **⚙️ Build & SDKs**: Opens the Build, Compilers & SDK Settings modal (with a live green/orange health status indicator dot).
- **📋 Copy Level**: Copies the level as 3D actor clipboard text.

---

### 3.3 Left Tools Sidebar (`#left-sidebar`)
Houses all 8 interactive viewport tools in a clean 2-column grid with hotkey badges:

| Tool Button | Hotkey | Function |
| :--- | :---: | :--- |
| **Move** | `W` or `V` | Selects objects and displays the 3D translation gizmo (`X`, `Y`, `Z` arrows). |
| **Rotate** | `E` | Selects objects and displays the 3D rotation gizmo (`90°` / `15°` snapped arcs). |
| **Rect** | `R` | Click and drag on the ground grid or on top of an existing block to draw a new rectangular block (`20 cm` initial height). |
| **Circle** | `C` | Click center and drag radius to draw a cylinder block (`20 cm` initial height). |
| **Push/Pull** | `P` | Click any block and drag vertically to extrude its height (`H`) up or down in real time with live centimeter readouts. |
| **Hole** | `H` | Activates the **Hole Cutter (C# CSG)** panel directly below the tools grid to subtract rectangular doors/windows, circular pipes, or arched doorways from the selected block. |
| **Sphere** | `S` | Prompts for a diameter in `cm` and spawns a sphere primitive at the origin. |
| **Light** | `L` | Spawns a Point Light actor (`Static`, `Stationary`, or `Movable`) with dynamic attenuation radius. |

#### Contextual Hole Cutter (C# CSG) Panel
When the **Hole (`H`)** tool is selected, the **Hole Cutter (C# CSG)** block appears in the Left Sidebar:
- **Cutout Shape**: `Rectangle (Door/Window)`, `Circle (Pipe/Vent)`, or `Arch (Rounded Top)`.
- **Cut Axis**: `Auto (Thinnest Wall Axis)`, `Cut Along Y (Front/Back Wall)`, `Cut Along X (Left/Right Wall)`, or `Cut Along Z (Floor/Ceiling)`.
- **Hole W / H (cm)**: Width and height of the opening (default `120 cm × 210 cm` standard doorway).
- **Offset Along Wall (cm)** & **Height From Base (cm)**: Precise placement along the wall (`0 cm` height from base cuts a floor-level doorway; `90 cm` cuts a window).
- **Cut Depth**: `Through All (Full Cut)` or `Blind Pocket / Recess (cm)` for wall niches and recessed panels.
- **✂️ Punch Hole in Selected**: Executes the C# boolean subtraction (`CsgEngine.cs`) and updates the 3D mesh in place.

---

### 3.4 Right Inspector & Forms Sidebar (`#sidebar`)
Organized into four vertical blocks:

1. **Forms (Architectural Presets)**:
   - 1-click spawn buttons for metric-scaled architectural primitives:
     - **Wall** (`400 × 20 × 300 cm`)
     - **Floor** (`400 × 400 × 20 cm`)
     - **Ramp** (`200 × 400 × 150 cm` sloped wedge)
     - **Cylinder** (`100 × 100 × 300 cm` column)
     - **Stairs** (`200 × 300 × 200 cm` 8-step staircase)
     - **Arch Door** (`300 × 30 × 300 cm` wall with pre-cut doorway)
2. **Selected Object Dimensions**:
   - Edit **Actor Name** (e.g., `SM_Wall_01`) and **Color Swatch** (automatically rebakes a procedural metric grid texture tinted to your chosen color).
   - Edit **Width (X)**, **Length (Y)**, and **Height (Z)** in centimeters.
   - Edit world coordinates **Pos X**, **Pos Y**, and **Pos Z** in centimeters.
   - Action buttons: **Duplicate (`Ctrl+D`)**, **Delete (`Del`)**, **💾 Save .OBJ** (saves selected mesh to `Assets/Meshes/`), and **🎬 Save .USD** (saves level stage to `Assets/Levels/`).
3. **Entity Components (ECS)**:
   - Displays all ECS components attached to the selected entity (`Transform`, `Mesh`, `Collider`, `Rigidbody`, `CharacterController`, `Script`, `AudioSource`).
   - Use the **+ Add Component...** dropdown at the bottom of the block to attach physics, character controllers, scripts, or 3D audio to any blockout object.
4. **Scene Outliner**:
   - Lists all actors and lights in the level with their dimensions in centimeters (`400×20×300`). Click any item to select and focus it.

---

### 3.5 Bottom Content Drawer & Asset Manager
Press **`Ctrl + Space`** or click the **📁 Content Drawer** pill at the bottom-left of the viewport:
- **Pin / Dock Button (`📌 Dock`)**: Pins the Content Drawer into the workspace grid below the 3D viewport so it stays open while you model.
- **Left Tree Pane**:
  - **Smart Collections**: `All Assets`, `Meshes (.obj)`, `USD Stages (.usd)`, `Scripts (.js/.cs)`.
  - **Project Folders**: `Assets`, `Assets/Meshes`, `Assets/Materials`, `Assets/Textures`, `Assets/Levels`, `Assets/Prefabs`, `Assets/Audio`, `Assets/Scripts`.
- **Top Filter Bar**: Search input + type filter pills (`All`, `Mesh`, `USDStage`, `Texture`, `Script`, `Audio`).
- **Center Asset Grid**: Displays thumbnails, asset names, type badges, and file sizes.
  - **Double-click** a `.obj` or `.usd` asset to stream its compiled binary geometry (`.beng`) directly into the 3D viewport.
  - **Right-click** any asset tile to open the context menu:
    - **➕ Place in Scene**
    - **🕸️ Reference Viewer** (opens an interactive SVG node graph showing parent/child USD and asset references)
    - **🔄 Reimport Asset**
    - **📂 Show in Explorer**
    - **📋 Copy Virtual Path** (e.g., `/Game/Meshes/SM_Wall.obj`)
    - **🗑️ Delete Asset**
- **Right Asset Details Pane**: Shows the persistent 32-character `.meta` GUID, virtual path, triangle/vertex counts, and import settings.

---

## 4. Keyboard & Mouse Shortcuts Reference

### 3D Viewport Navigation (Editor Mode)
| Action | Input |
| :--- | :--- |
| **Select Actor** | `Left Mouse Click` on mesh |
| **Orbit Camera** | `Right Mouse Drag` (or `Alt + Left Mouse Drag`) |
| **Pan Camera** | `Middle Mouse Drag` (or `Shift + Right Mouse Drag`) |
| **Zoom Camera** | `Mouse Wheel Scroll` |

### Play-In-Editor (PIE) Controls
| Action | Input |
| :--- | :--- |
| **Move Player** | `W`, `A`, `S`, `D` |
| **Look Around** | `Mouse Movement` (Pointer Lock active) |
| **Jump** | `Spacebar` |
| **Release Mouse / Exit PIE** | `Esc` (or click **⏹ Stop** in Top Toolbar) |

### Global Application Hotkeys
| Shortcut | Command |
| :--- | :--- |
| `W` or `V` | Activate **Move (Translate)** Tool |
| `E` | Activate **Rotate** Tool |
| `R` | Activate **Draw Rectangle** Tool |
| `C` | Activate **Draw Circle** Tool |
| `P` | Activate **Push/Pull (Extrude Height)** Tool |
| `H` | Activate **Hole Cutter (CSG Boolean)** Tool |
| `S` | **Spawn Sphere** (prompts for diameter in cm) |
| `L` | **Spawn Point Light** |
| `Delete` | **Delete Selected** Actor |
| `Ctrl + Z` | **Undo** |
| `Ctrl + Y` / `Ctrl + Shift + Z` | **Redo** |
| `Ctrl + D` | **Duplicate Selected** Actor |
| `Ctrl + C` | **Copy Level to Clipboard** |
| `Ctrl + N` | **New Project...** |
| `Ctrl + Shift + O` | **Open Project Folder...** |
| `Ctrl + S` | **Save Project (`.json`)** |
| `Ctrl + O` | **Load Project (`.json`)** |
| `Ctrl + Space` | **Toggle Content Drawer** |
| `Ctrl + B` or `F7` | **Open Build, Compilers & SDK Settings Modal** |

---

## 5. Core Workflows (Step-by-Step)

### Workflow 1: Sketching, Extruding & Cutting Holes (CSG)
1. Select the **Rect (`R`)** tool from the Left Sidebar.
2. Click and drag on the ground grid (or on top of an existing floor) to sketch the footprint of a room or wall. A live blue measurement badge displays the exact `W × L cm` dimensions as you drag.
3. Switch to the **Push/Pull (`P`)** tool, click your new block, and drag upward until the live badge reads `Height: 300 cm`.
4. To cut a doorway into the wall, select the wall and click the **Hole (`H`)** tool on the Left Sidebar.
5. In the **Hole Cutter (C# CSG)** panel, set **Shape** to `Arch (Rounded Top)`, **Hole W** to `120`, **Hole H** to `220`, **Height From Base** to `0`, and click **✂️ Punch Hole in Selected**.
6. The C# CSG engine performs a 3D boolean subtraction and replaces the wall with a watertight mesh containing the arched doorway.

### Workflow 2: Exporting Levels (Clipboard / OBJ / USD)
- **Method A — Instant Clipboard Copy**:
  1. Click **📋 Copy Level** in the Top Toolbar (or press `Ctrl+C`).
  2. Paste directly into your target engine or clipboard-compatible editor (`Ctrl+V`).
  3. Every blockout actor and light spawns at its exact centimeter coordinates and scale.
- **Method B — Native `.OBJ` or `.USD` Export**:
  1. Open **File $\rightarrow$ Export Level (.OBJ)** or **File $\rightarrow$ Export Level (.USD)**.
  2. Choose a destination file. The exported `.obj` or `.usda` file includes `metersPerUnit = 0.01` (`1 cm`) and `upAxis = "Z"`, importing 1:1 into standard 3D suites without axis flipping.

### Workflow 3: Managing Projects, `.OBJ`/`.USD` Assets & Reference Graphs
1. Click **File $\rightarrow$ New Project...** (`Ctrl+N`), enter a project name (e.g., `SciFiOutpost`), and choose a parent folder.
2. Select any custom CSG block in your level and click **💾 Save .OBJ** in the Right Sidebar (or **File $\rightarrow$ Save Selected as .OBJ**).
3. Press `Ctrl+Space` to open the **Content Drawer**. Your new mesh appears in `Assets/Meshes/` with a persistent `.meta` GUID.
4. Right-click the asset tile and choose **🕸️ Reference Viewer** to inspect its dependency graph, or double-click the tile at any time to spawn additional instances streamed from the binary `.beng` cache.

### Workflow 4: ECS Gameplay Components & Play-In-Editor (PIE)
1. Select any object in the scene (or create a block for a moving platform or trigger).
2. Scroll to the **Entity Components (ECS)** section in the Right Sidebar.
3. Use **+ Add Component...** to attach:
   - **`Collider`**: Choose `Box`, `Sphere`, `Capsule`, or `Mesh`, or mark it as `Is Trigger`.
   - **`Rigidbody`**: Set `Body Type` (`Static`, `Kinematic`, or `Dynamic`), `Mass (kg)`, and `Use Gravity`.
   - **`CharacterController`**: Pre-configured to human metrics (`Height: 180 cm`, `Radius: 30 cm`, `Step Offset: 45 cm`, `Move Speed: 500 cm/s`, `Jump Speed: 450 cm/s`).
   - **`Script`**: Select a gameplay script from `Assets/Scripts/` (`Rotator.js`, `DoorTrigger.js`, `PlayerMovement.js`) and customize its JSON parameters.
   - **`AudioSource`**: Configure 3D spatial WebAudio synthesis (`AmbientHum`, `Beep`, `Wind`, `Pulse`) with min/max distance falloff.
4. Click **▶ Play** in the Top Toolbar to enter **Play-In-Editor (PIE)** mode. Walk around your blockout in first-person (`WASD` + `Mouse` + `Space` to jump), climb stairs up to `45 cm` step height, and test triggers. Click **⏹ Stop** (or press `Esc`) to return to the editor with your scene restored to its exact pre-Play state.

### Workflow 5: Baking Ambient Occlusion & 4-Tier Scalability
1. Click **Bake AO** in the Top Toolbar. The backend executes ray-marched Vertex Ambient Occlusion across all meshes (using Vulkan GPU Compute if available, or C++20 AVX2 / C# SIMD fallback) and applies soft contact shadowing to corners and crevices.
2. Use the **Scalability Switcher (`Low`, `Med`, `High`, `Epic`)** in the Top Toolbar to preview how your level renders across hardware targets:
   - **Low (Tier 0 — Mobile Low):** `0.75x` resolution scale, Lambert + Baked Vertex AO, single `1024px` shadow map.
   - **Med (Tier 1 — Mobile High):** `1.0x` resolution scale, PBR-Lite, `1024px` shadows, up to 2 dynamic shadow lights.
   - **High (Tier 2 — Desktop PC):** Full Cook-Torrance PBR, ACES Filmic Tone Mapping, `2048px` shadows, up to 4 shadow lights.
   - **Epic (Tier 3 — Console / High-End PC):** `1.25x` supersampling, `4096px` shadows, volumetric fog, up to 8 shadow lights.
3. Click **GPU HUD** in the Top Toolbar to monitor real-time FPS, frame time (`ms`), draw calls, triangles, `THREE.InstancedMesh` batches, and `THREE.LOD` objects.

### Workflow 6: Compiling C++20 Plugins, Packaging Builds & ADB Deployment
1. Press **`Ctrl + B`** (or click **⚙️ Build & SDKs** in the Top Toolbar).
2. **Tab 1 — Native SDKs & Health**:
   - Inspect live probe status for **Vulkan SDK**, **Windows 11 SDK & MSVC (`cl.exe`)**, **Linux GCC/G++**, and **Android SDK / NDK (`adb`, `aapt2`)**.
   - Optionally configure custom SDK folder paths and click **💾 Save & Re-Probe SDKs** (persisted to `ProjectSettings/SDKPaths.json`).
3. **Tab 2 — Native C++20 Compiler**:
   - Place custom C++20 files inside `<ProjectRoot>/Assets/NativePlugins/` (a starter `GamePlugin.cpp` is automatically generated for new projects).
   - Select Target Platform (`Windows x64` or `Linux x64`), Optimization Level (`/O2`, `/Od`, `/Ox`), and AVX2 SIMD, then click **🔨 Compile Native C++20 Plugin**.
   - Live compiler output streams into the dark console and outputs `Binaries/Win64/GameNative.dll` (or `Binaries/Linux64/libGameNative.so`).
4. **Tab 3 — Platform Packaging & ADB**:
   - Choose Target Platform (**Windows 64-bit**, **Linux 64-bit**, or **Android APK**), Default Scalability Tier, and whether to cook SPIR-V shaders and `.beng` binary meshes.
   - Click **📦 Package Standalone Build** to stage a complete build in `<ProjectRoot>/Saved/StagedBuilds/<Platform>/`.
   - For Android, connect a phone/headset with USB Debugging enabled, click **🔄 Refresh ADB**, select your device, and click **📲 Build & Deploy to Android** to install and launch the APK automatically.

