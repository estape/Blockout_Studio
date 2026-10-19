# Blockout Studio

[![License: MPL-2.0](https://img.shields.io/badge/License-MPL_2.0-blue.svg)](LICENSE.md)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![C++20 SIMD](https://img.shields.io/badge/C%2B%2B-20%20AVX2-00599C.svg)](Native/EngineNative/)
[![Three.js](https://img.shields.io/badge/Three.js-WebGL_2.0-049EF4.svg)](https://threejs.org/)
[![Vulkan SDK](https://img.shields.io/badge/Vulkan-SDK_Compute-red.svg)](https://vulkan.lunarg.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20Android-green.svg)]()

**Blockout Studio** is a fast, native-accelerated 3D level prototyping, architectural blockout, CSG boolean modeling, and ECS playtesting engine engineered with **.NET 10**, **Photino.NET**, **Three.js (WebGL 2.0)**, and **C++20 SIMD AVX2**.

Designed for game designers, level architects, and developers, Blockout Studio delivers instant 3D level blocking, boolean hole cutting, non-destructive OpenUSD/OBJ workflows, and immediate first-person playtesting without the overhead of heavy commercial game engine editors.

---

## 📸 Overview & Key Features

- 📐 **Metric-Accurate 3D Blockout (`1 unit = 1 cm`, Z-Up)**
  Sketch rectangles and circles, extrude geometric heights with Push/Pull, spawn architectural primitives (*Wall*, *Floor*, *Ramp*, *Cylinder*, *Stairs*, *Arch Door*), and measure spatial scale against the interactive **180cm Dummy**.
- 🕳️ **Real-Time C# CSG Boolean Subtraction**
  Cut rectangular, circular, or arched openings through walls and geometry in real-time, with full support for through-cuts, blind pocket cuts, and reversible history snapshots.
- 🧍 **Interactive 180cm Dummy & Player Prototyping**
  Select, move, and rotate the reference dummy directly in the 3D viewport using transform gizmos or precise inspector coordinates.
- 🎮 **ECS Runtime & Play-In-Editor (PIE)**
  Attach `Collider`, `Rigidbody`, `CharacterController` (`180 cm` dummy metrics), `AudioSource`, and `Script` components (`Rotator.js`, `DoorTrigger.js`, etc.) and jump immediately into first-person playtesting (`▶ Play`).
- 📁 **Asset Database & Non-Destructive Stage Workflows**
  Persistent Unity-style `.meta` GUID sidecars, binary mesh cache (`.beng`), high-speed native `.obj` parsing, and OpenUSD (`.usda`) non-destructive `over` composition for seamless handoff to game engines and DCC tools.
- ⚡ **Vulkan SDK & SIMD Acceleration**
  Bake Vertex Ambient Occlusion via GPU compute shaders or .NET 10 / C++20 AVX2 SIMD routines. Compile GLSL to SPIR-V with `glslc` and optimize with `spirv-opt`.
- 🎛️ **4-Tier Scalability & Auto-LOD Generation**
  Dynamically switch between **Tier 0 (Mobile Low)**, **Tier 1 (Mobile High)**, **Tier 2 (Desktop PC)**, and **Tier 3 (Cinematic)** presets with real-time automatic mesh simplification (LOD0 $\rightarrow$ LOD1 $\rightarrow$ LOD2).
- 🛠️ **Direct Native SDK Manager & Multi-Platform Packaging (`Ctrl+B`)**
  Direct host detection for **LunarG Vulkan SDK**, **Windows 11 SDK + MSVC (`cl.exe`)**, **Linux GCC/G++**, and **Android SDK/NDK (`adb`, `aapt2`)**. Compile custom C++20 plugins (`GameNative.dll`) and package standalone Windows, Linux, and Android APK builds with 1-click `adb` deployment.
- 🖥️ **Desktop Ergonomics**
  Native top menu bar (`File`, `Edit`, `Assets`, `Tools`, `Build`, `View`), dedicated Left Tools sidebar, resizable column splitters with `localStorage` persistence, and startup Welcome Screen modal.

---

## 🛠️ Technology Stack

| Layer | Technologies | Purpose |
| :--- | :--- | :--- |
| **Host Runtime** | **.NET 10 (C# 13)** | Core host, dependency injection, service container, SIMD math, and CSG engine |
| **Window & IPC** | **Photino.NET** | Ultra-lightweight native desktop windowing (Chromium WebView2 / WebKitGTK) and bidirectional local IPC router |
| **3D Rendering** | **Three.js (WebGL 2.0)** | Viewport rendering, hardware-accelerated Direct3D 11 ANGLE backend on Windows, Vulkan ANGLE on Linux |
| **Native Core** | **C++20 & AVX2 SIMD** | High-performance geometry processing, normal calculation, and ray-triangle intersection via P/Invoke (`[LibraryImport]`) |
| **Shader Pipeline**| **LunarG Vulkan SDK** | SPIR-V compute shader compilation (`glslc`), optimization (`spirv-opt`), and transpilation (`spirv-cross`) |
| **Asset Formats** | **USD & Wavefront OBJ** | Native `.usda` stage generator with `over` opinion layers, binary `.beng` cache, and `.meta` GUID sidecars |
| **Gameplay** | **ES6 Entity-Component-System** | First-person character controller, physics collision (AABB/Sphere/Capsule), audio, and JS scripting |

---

## 🚀 Getting Started & How to Compile

### 1. Prerequisites

- **Required**:
  - [.NET 10.0 SDK](https://dotnet.microsoft.com/download) (or later).
- **Optional (for Native C++20 Plugin Compilation)**:
  - **Windows**: [Visual Studio 2022](https://visualstudio.microsoft.com/) or Visual Studio Build Tools with the *Desktop development with C++* workload (provides MSVC `cl.exe` with C++20 support).
  - **Linux**: `g++` (GCC 12+), `make` or `ninja`, `libgtk-3-dev`, `libwebkit2gtk-4.1-dev`.
- **Optional (for GPU Compute Shader Baking)**:
  - [LunarG Vulkan SDK](https://vulkan.lunarg.com/) ($\ge 1.3$) with `glslc` and `spirv-opt` added to your system `PATH`.
- **Optional (for Android APK Packaging & Deployment)**:
  - Android SDK (`build-tools`, `platform-tools/adb`, `platforms/android-34`) and Android NDK.

---

### 2. Clone and Run

```bash
# Clone the repository (alpha branch)
git clone -b alpha https://github.com/estape/Blockout_Studio.git
cd Blockout_Studio

# Restore NuGet dependencies and launch the studio
dotnet run
```

---

### 3. Run the Automated Test Suite

Blockout Studio includes an internal 25-suite automated test and diagnostic harness validating all backend subsystems:

```bash
dotnet run -- --test
```

This verifies:
- CSG boolean subtraction and vertex generation
- SIMD and Vulkan Compute Ambient Occlusion baking
- Toolchain discovery (Vulkan SDK, MSVC `cl.exe`, Android CLI)
- Native OBJ roundtrip and USD stage generation
- Native C++20 SIMD interop (`EngineNative.dll`)
- Binary mesh compilation (`.beng`) and artifact cache
- 4-Tier Scalability presets and auto-LOD reduction
- ECS collision math and mannequin spatial metrics
- Native C++20 MSVC compiler pipeline and platform packaging
- Dependency Injection container and all 32 IPC dispatch handlers

---

### 4. Compiling the Native C++20 Core (`EngineNative`)

The engine includes pre-built native binaries in `Native/EngineNative/`. To recompile the native SIMD library from source:

#### On Windows (MSVC):
```cmd
cd Native\EngineNative
build_msvc.cmd
cd ..\..
```
*(Produces `EngineNative.dll` and `EngineNative.lib` optimized with `/O2 /Oi /GL /arch:AVX2 /std:c++20`)*.

#### On Linux (GCC):
```bash
cd Native/EngineNative
g++ -O3 -march=x86-64-v3 -flto -fPIC -shared -std=c++20 src/EngineNative.cpp -o libEngineNative.so
cd ../..
```

---

### 5. Packaging Standalone Builds

Open the **Build, Compilers & SDK Settings** modal in the engine using `Ctrl+B` or **Build $\rightarrow$ Build Settings...**:
1. Check the detected SDKs (Vulkan SDK, Windows SDK/MSVC, Linux GCC, Android NDK).
2. Choose your target platform (**Windows x64**, **Linux x64**, or **Android APK**).
3. Click **Package Game**:
   - Binaries are staged into `<ProjectRoot>/Saved/StagedBuilds/<Platform>/`.
   - Windows produces `BlockoutStudio.exe`, native DLLs, cooked shaders, and content.
   - Android produces `BlockoutGame.apk` and deploys automatically via `adb install -r`.

---

## 📁 Repository Structure

```
Blockout_Studio/
├── Program.cs                  # Engine entry point, DI container & Photino window host
├── BlockoutStudio.csproj       # .NET 10 project definition & NuGet dependencies
├── PROJECT_CONTEXT.md          # Single Source of Truth (SSOT) & Agent Memory Base
├── LICENSE.md                  # Mozilla Public License 2.0 & Third-Party Notices
├── SECURITY.md                 # GitHub Security Policy & Vulnerability Reporting
├── Services/
│   ├── Assets/                 # Asset database, .meta sidecars & registry indexer
│   ├── BuildPipeline/          # Native compiler, SDK detector & platform packager
│   ├── Geometry/               # CSG engine, OBJ/USD stages, LOD generator & SIMD AO
│   ├── Graphics/               # Vulkan SDK SPIR-V compute pipeline
│   ├── Hosting/                # WebView2 / ANGLE GPU configurator
│   ├── Ipc/                    # Typed IPC Router & 32 modular action handlers
│   ├── NativeInterop/          # C# [LibraryImport] bindings to EngineNative.dll
│   └── Toolchains/             # Vulkan, MSVC, and Android CLI discovery
├── Native/
│   └── EngineNative/           # C++20 AVX2 SIMD source, headers & build scripts
├── docs/
│   ├── USER_GUIDE.md           # User manual, UI tour, shortcuts & workflows
│   └── TECHNICAL_REFERENCE.md  # Architectural deep-dive, IPC specs & ABI reference
└── wwwroot/                    # HTML5 / WebGL 2.0 Editor Frontend
    ├── index.html              # Studio UI, top menu bar, left tools & viewport
    ├── css/main.css            # Responsive CSS grid layout, theme & modal styles
    └── js/                     # ES6 frontend modules, Three.js engine & ECS systems
```

---

## 📖 Documentation Index

For in-depth guides and architectural references, explore the documentation suite:

| Document | Purpose |
| :--- | :--- |
| **[`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md)** | **Single Source of Truth (SSOT)** — Architecture patterns, "Never-Break" rules, ADR log, and handover state. |
| **[`docs/USER_GUIDE.md`](docs/USER_GUIDE.md)** | **User Guide** — Complete UI tour, keyboard shortcuts, modeling tools, PIE mode, and packaging tutorials. |
| **[`docs/TECHNICAL_REFERENCE.md`](docs/TECHNICAL_REFERENCE.md)** | **Technical Reference** — C# services, 32 IPC handlers, C++20 ABI exports, `.beng` specs, and SDK pipelines. |

---

## ⚖️ License

Blockout Studio is open-source software licensed under the **[Mozilla Public License Version 2.0 (MPL-2.0)](LICENSE.md)**.

### Third-Party Software Notices
Blockout Studio builds upon and interfaces with external open-source software:
- **Photino.NET**: Licensed under the **[Apache License 2.0](http://www.apache.org/licenses/LICENSE-2.0)**.
- **LunarG Vulkan SDK / Khronos Vulkan**: Licensed under **[Apache License 2.0 / MIT](https://vulkan.lunarg.com/)**.
- **Three.js**: Licensed under the **[MIT License](https://github.com/mrdoob/three.js/blob/dev/LICENSE)**.
- **Microsoft .NET Runtime**: Licensed under the **[MIT License](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)**.

See [`LICENSE.md`](LICENSE.md) for full license texts and notices.

---

## 🛡️ Security Policy

For security guidelines, vulnerability reporting procedures, and our threat model, please refer to our **[Security Policy](SECURITY.md)**.



