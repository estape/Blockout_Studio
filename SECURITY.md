# Security Policy

## Supported Versions

Security updates and patches are actively maintained for the following branches and pre-releases:

| Version / Branch | Supported | Notes |
| :--- | :---: | :--- |
| **`alpha` (active development)** | ✅ Yes | Current active branch receiving all security updates and fixes |
| **Latest Tagged Pre-release** | ✅ Yes | Latest release milestone tag on GitHub |
| `< 1.0.0` (older commits/snapshots) | ❌ No | Historical commits do not receive backported patches |

---

## Reporting a Vulnerability

We take the security of Blockout Studio and its community seriously. If you discover a security vulnerability, **please do not disclose it publicly via issue trackers, public discussions, or social media.** 

Please use our coordinated private disclosure process:

### 1. Primary Reporting Channel: GitHub Private Vulnerability Reporting
1. Go to the [Blockout Studio Repository Security Tab](https://github.com/estape/Blockout_Studio/security).
2. Click **"Report a vulnerability"** under **Advisories**.
3. Provide a descriptive title, affected component, proof of concept, and impact assessment.
4. This keeps the report private between you and the repository maintainers while a patch is prepared.

### 2. Fallback Reporting Channel
If GitHub Private Vulnerability Reporting is unavailable or inaccessible:
- Contact the repository maintainers directly through their [GitHub Profile](https://github.com/estape) or initiate contact by opening a confidential discussion with the project maintainers.
- *Note: To protect maintainers from automated spam and phishing, personal email addresses are not listed publicly.*

---

## What to Include in Your Report

To help us investigate and triage your report as quickly as possible, please include:
- **Vulnerability Category**: (e.g., Arbitrary Code Execution, Local IPC Hijacking, Path Traversal, Native Memory Corruption, Insecure Deserialization).
- **Impacted Subsystem**: (e.g., `Services/BuildPipeline/NativeCompilerService.cs`, `Services/Ipc/`, `Native/EngineNative/`, `wwwroot/js/ipcBridge.js`).
- **Environment**: Host OS (Windows 11, Linux distribution, Android version), compiler version, and .NET runtime version.
- **Reproduction Steps**: Step-by-step instructions or a minimal reproducible example project.
- **Proof of Concept (PoC)**: Minimal sample file (`.obj`, `.usda`, `.beng`, or IPC payload) triggering the issue.
- **Estimated Severity**: Description of what an attacker could accomplish if exploited.

---

## Response Timeline & SLA

We adhere to a standard responsible disclosure timeline:

- **Acknowledgment**: We aim to acknowledge receipt of your vulnerability report within **48 to 72 hours**.
- **Initial Assessment**: Within **7 business days**, we will investigate, verify the issue, and provide an initial severity assessment and progress update.
- **Fix & Remediation**: Maintainers will work on a patch in a private fork/branch and verify the fix across supported platforms.
- **Coordinated Disclosure**: We follow a standard **90-day coordinated disclosure timeline**. A public GitHub Security Advisory (and CVE if applicable) will be published simultaneously with the patch release, or sooner by mutual agreement once a fix is deployed.

---

## Researcher Recognition & Bounty Policy

- **Public Credit & Acknowledgments**: Valid security reports that follow this coordinated disclosure policy will be publicly credited in the published GitHub Security Advisory and in the engine's release patch notes with the researcher's name or handle (unless anonymity is requested).
- **Bug Bounty Notice**: Blockout Studio is an independent, open-source project and does not currently operate a monetary bug bounty program or offer financial rewards. We deeply appreciate the voluntary contributions and diligence of the security research community.

---

## Threat Model & Security Boundaries

When using or developing for Blockout Studio, please observe the following core security boundaries:

### ⚠️ Native C++ Plugin Execution Safety
- Blockout Studio features a native compiler pipeline (`Services/BuildPipeline/NativeCompilerService.cs`) that automatically invokes host compilers (`cl.exe` on Windows, `g++` on Linux) to compile C++20 code inside `<ProjectRoot>/Assets/NativePlugins/` into dynamic libraries (`GameNative.dll` / `libGameNative.so`) and loads them into the engine process via P/Invoke `[LibraryImport]`.
- **Policy**: **Only open and compile Blockout Studio projects from trusted authors.** Loading and executing arbitrary C++ dynamic libraries runs with the full OS permissions and privileges of the host user process. Opening an untrusted project containing malicious native source code or pre-compiled binaries can compromise your system.

### 🛡️ Local IPC & WebView Isolation
- Communication between the web frontend UI and the .NET 10 host is routed strictly over Photino's local message pipe (`window.external.sendMessage`).
- The internal webview does not navigate to external untrusted web origins, and all incoming messages are strictly validated through typed `IIpcActionHandler` implementations.

### 📁 Workspace Boundary Enforcement
- Asset imports, USD stage exports, and artifact caching (`Library/ArtifactCache/`) are restricted to the active project workspace root.
- Path traversal sequences (`../`) in asset registry paths and project manifests are rejected to prevent accessing arbitrary system files.

---

## Safe Harbor

We consider security research conducted under this policy to be authorized. We will not pursue civil or criminal action against researchers who:
- Make a good faith effort to avoid privacy violations, data destruction, and service disruption.
- Give maintainers reasonable time to remediate vulnerabilities before public disclosure.
- Comply with all applicable laws and do not exploit security issues beyond what is necessary to demonstrate a proof of concept.
