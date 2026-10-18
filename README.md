# UE5 Blockout Studio (.NET 10 + C++20 SIMD + Vulkan Compute + WebGL 2.0)

**UE5 Blockout Studio** (`UE5BlockoutStudio`) is a native-accelerated 3D level blockout, CSG boolean modeling, ECS gameplay prototyping, and multi-platform packaging studio engineered for **Unreal Engine 5**, Desktop PC, and Mobile (Android) game development.

---

## 📚 Documentation Index

| Document | Purpose | Audience |
| :--- | :--- | :--- |
| **[`PROJECT_CONTEXT.md`](file:///D:/Rodrigo/Documents/Dev/vscode/Desktop/dotNET/Blockout_Studio/PROJECT_CONTEXT.md)** | **Single Source of Truth (SSOT) & Agent Memory Base** — System overview, stack, code conventions, "Never-Break" rules, Architectural Decision Log (ADR), and AI handover state. | AI Agents & Project Architects |
| **[`docs/USER_GUIDE.md`](file:///D:/Rodrigo/Documents/Dev/vscode/Desktop/dotNET/Blockout_Studio/docs/USER_GUIDE.md)** | **User Guide & Manual** — Complete tour of the Top Menu Bar, Left Tools Sidebar, Right Inspector, Content Drawer, hotkeys, and step-by-step modeling, PIE, and packaging workflows. | Level Designers, Artists & Users |
| **[`docs/TECHNICAL_REFERENCE.md`](file:///D:/Rodrigo/Documents/Dev/vscode/Desktop/dotNET/Blockout_Studio/docs/TECHNICAL_REFERENCE.md)** | **Technical Reference & Maintenance Manual** — Detailed catalog of all C# services, 32 IPC handlers, Native C++20 `EngineNative` ABI, ES6 modules, `.beng` binary format, and SDK pipelines. | Software Engineers & Maintainers |

---

## 🚀 Quick Start

### 1. Launch the Desktop Studio
```powershell
dotnet run
```

### 2. Run the 25-Suite Automated Test & Diagnostic Runner
```powershell
dotnet run -- --test
```

---

## ✨ Key Capabilities
- **Metric-Accurate UE5 Blockout (`1 Unit = 1 cm`, Z-Up)**: Sketch rectangles/circles, extrude heights with Push/Pull, spawn architectural presets (`Wall`, `Floor`, `Ramp`, `Cylinder`, `Stairs`, `Arch Door`), and cut boolean openings (`Rect`, `Circle`, `Arch`, blind pockets or through-cuts) via C# CSG.
- **1-Click Unreal Engine 5 Bridge**: Copy the entire scene as UE5 `.t3d` Actor text (`Ctrl+C`) and paste (`Ctrl+V`) directly into the Unreal Engine 5 viewport, or export native `.obj` and `.usda` stages.
- **Hybrid Unity + UE5 Asset Manager (`Ctrl+Space`)**: Combines persistent `.meta` GUID sidecars with `Library/AssetRegistry.bin`, binary `.beng` mesh caching, non-destructive USD `over` delta layers, and an interactive SVG Reference Viewer.
- **Vulkan SDK & SIMD Acceleration + 4-Tier Scalability**: Compiles GLSL to SPIR-V (`glslc` $\rightarrow$ `spirv-opt` $\rightarrow$ `spirv-cross`), bakes Vertex Ambient Occlusion via GPU/AVX2/SIMD, and switches live between **Tier 0 (Mobile Low)**, **Tier 1 (Mobile High)**, **Tier 2 (Desktop PC)**, and **Tier 3 (Console / Epic)**.
- **ECS Gameplay Runtime & Play-In-Editor (PIE)**: Attach `Collider`, `Rigidbody`, `CharacterController` (`180 cm` UE5 Mannequin metrics), `Script`, and `AudioSource` components and playtest immediately in first-person (`▶ Play`).
- **Direct Native SDK Manager & Compiler Pipeline (`Ctrl+B` / `F7`)**: Probes LunarG Vulkan SDK, Windows 11 SDK + MSVC (`cl.exe`), Linux GCC/G++, and Android SDK/NDK (`adb`, `aapt2`), compiles custom C++20 plugins (`GameNative.dll`), packages standalone Windows/Linux/Android APK builds, and deploys 1-click via `adb`.

