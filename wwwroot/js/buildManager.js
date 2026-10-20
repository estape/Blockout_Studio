/**
 * @file buildManager.js
 * @brief Native SDK health monitoring, C++20 compiler invocation, multi-platform packaging, and ADB deployment.
 */

// ==============================================================================
// buildManager.js — Native SDKs, C++20 Compiler & Multi-Platform Builder Module
// ==============================================================================

import { sendIpcMessage, showToast } from './ipcBridge.js';

/** @type {Object|null} Cached SDK detection report from the backend. */
let currentSdkReport = null;
/** @type {Array<Object>} List of currently connected ADB devices. */
let currentAdbDevices = [];
/** @type {boolean} Flag indicating whether native compilation is actively running. */
let isCompiling = false;
/** @type {boolean} Flag indicating whether game packaging is actively running. */
let isPackaging = false;

/**
 * @brief Initializes the build manager and attaches IPC message listeners.
 */
export function initBuildManager() {
    window.addEventListener('ipc-message', onIpcMessage);
}

/**
 * @brief Handles incoming IPC messages related to SDK detection, compilation, and packaging.
 * @param {CustomEvent} e Custom event carrying parsed IPC payload detail.
 */
function onIpcMessage(e) {
    const data = e.detail;
    if (!data || !data.type) return;

    switch (data.type) {
        case 'sdk_status_response':
            currentSdkReport = data.report;
            renderSdkHealthTab(data.report);
            updateTopbarSdkBadge(data.report);
            break;

        case 'sdk_paths_saved':
            currentSdkReport = data.report;
            renderSdkHealthTab(data.report);
            updateTopbarSdkBadge(data.report);
            showToast('SDK Paths updated and re-probed.');
            break;

        case 'adb_devices_response':
            currentAdbDevices = data.devices || [];
            renderAdbDevices(currentAdbDevices);
            break;

        case 'compiler_log':
            appendCompilerLog(data.line);
            break;

        case 'compile_native_module_response':
            onCompileFinished(data.result);
            break;

        case 'build_log':
            appendPackageLog(data.line);
            break;

        case 'build_project_response':
            onPackageFinished(data.result);
            break;

        case 'deploy_log':
            appendPackageLog(`[DEPLOY] ${data.line}`);
            break;

        case 'deploy_to_device_response':
            if (data.success) {
                showToast('🚀 App deployed and launched successfully on Android device!');
            } else {
                showToast('❌ Deployment failed. Check package logs.');
            }
            break;
    }
}

/**
 * @brief Displays the Build & Packaging modal dialog and switches to a specified tab.
 * @param {string} [tabName='sdks'] Initial tab to activate ('sdks', 'compiler', or 'package').
 */
export function openBuildModal(tabName = 'sdks') {
    const modal = document.getElementById('build-modal');
    if (!modal) return;
    modal.style.display = 'flex';
    switchBuildTab(tabName);

    // Request latest SDK status
    sendIpcMessage('get_sdk_status', {
        projectRoot: window.activeProjectRoot || ''
    });

    sendIpcMessage('get_adb_devices', {});
}

/**
 * @brief Closes the Build & Packaging modal dialog.
 */
export function closeBuildModal() {
    const modal = document.getElementById('build-modal');
    if (!modal) return;
    modal.style.display = 'none';
}

/**
 * @brief Switches the active view pane within the Build modal dialog.
 * @param {string} tabName Target tab identifier ('sdks', 'compiler', or 'package').
 */
export function switchBuildTab(tabName) {
    document.querySelectorAll('.build-tab-btn').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.tab === tabName);
    });

    document.querySelectorAll('.build-tab-pane').forEach(pane => {
        pane.style.display = pane.id === `tab-pane-${tabName}` ? 'block' : 'none';
    });
}

/**
 * @brief Updates the status dot indicator in the top application bar reflecting SDK readiness.
 * @param {Object} report SDK health detection report.
 */
function updateTopbarSdkBadge(report) {
    const badge = document.getElementById('topbar-sdk-badge');
    if (!badge || !report) return;

    const msvcOk = report.msvc && report.msvc.available;
    const vulkanOk = report.vulkan && report.vulkan.available;

    if (msvcOk && vulkanOk) {
        badge.className = 'status-dot dot-ready';
        badge.title = `Native SDKs Ready: Vulkan ${report.vulkan.version || 'Active'} & MSVC ${report.msvc.version || 'Active'}`;
    } else if (vulkanOk || msvcOk) {
        badge.className = 'status-dot dot-partial';
        badge.title = 'Partial SDKs available (Vulkan or MSVC)';
    } else {
        badge.className = 'status-dot dot-missing';
        badge.title = 'Native C++ & Vulkan SDKs not detected';
    }
}

/**
 * @brief Renders the visual status cards for Vulkan, MSVC, Linux GCC, and Android toolchains.
 * @param {Object} report SDK health detection report.
 */
function renderSdkHealthTab(report) {
    if (!report) return;

    // Vulkan Card
    const vCard = document.getElementById('sdk-card-vulkan');
    if (vCard) {
        const v = report.vulkan;
        vCard.innerHTML = `
            <div class="sdk-card-header">
                <div class="sdk-title-group">
                    <span class="sdk-icon">🌋</span>
                    <div>
                        <strong class="sdk-name">Vulkan SDK & Runtime</strong>
                        <div class="sdk-version">${v.version ? `Version ${v.version}` : 'Version 1.4+'}</div>
                    </div>
                </div>
                <span class="badge ${v.available ? 'badge-success' : 'badge-warn'}">${v.available ? 'Active & Ready' : 'Not Found'}</span>
            </div>
            <div class="sdk-card-body">
                <div class="sdk-prop-row"><span class="sdk-prop-label">Path:</span> <span class="sdk-prop-val">${v.sdkPath || 'System Default / PATH'}</span></div>
                <div class="sdk-prop-row"><span class="sdk-prop-label">Headers:</span> <span class="sdk-prop-val">${v.hasHeaders ? '✅ vulkan/vulkan.h detected' : '❌ Headers missing'}</span></div>
                <div class="sdk-prop-row"><span class="sdk-prop-label">Libraries:</span> <span class="sdk-prop-val">${v.hasLibs ? '✅ vulkan-1.lib available' : '❌ Lib missing'}</span></div>
                <div class="sdk-prop-row"><span class="sdk-prop-label">Tools:</span> <span class="sdk-prop-val">${v.tools && v.tools.length > 0 ? v.tools.join(', ') : 'None'}</span></div>
                <div class="sdk-status-msg">${v.statusMessage}</div>
            </div>
        `;
    }

    // Windows SDK & MSVC Card
    const mCard = document.getElementById('sdk-card-msvc');
    if (mCard) {
        const m = report.msvc;
        const w = report.windows;
        mCard.innerHTML = `
            <div class="sdk-card-header">
                <div class="sdk-title-group">
                    <span class="sdk-icon">🪟</span>
                    <div>
                        <strong class="sdk-name">Windows 11 SDK & MSVC C++20</strong>
                        <div class="sdk-version">MSVC ${m.version || 'N/A'} | WinSDK ${w.version || 'N/A'}</div>
                    </div>
                </div>
                <span class="badge ${m.available ? 'badge-success' : 'badge-danger'}">${m.available ? 'Compiler Ready' : 'Missing'}</span>
            </div>
            <div class="sdk-card-body">
                <div class="sdk-prop-row"><span class="sdk-prop-label">Visual Studio / BuildTools:</span> <span class="sdk-prop-val">${m.installationPath || 'Not installed'}</span></div>
                <div class="sdk-prop-row"><span class="sdk-prop-label">Compiler (cl.exe):</span> <span class="sdk-prop-val">${m.clPath ? '✅ ' + m.clPath : '❌ cl.exe missing'}</span></div>
                <div class="sdk-prop-row"><span class="sdk-prop-label">Environment (vcvars64.bat):</span> <span class="sdk-prop-val">${m.vcVarsPath ? '✅ ' + m.vcVarsPath : '❌ Missing'}</span></div>
                <div class="sdk-prop-row"><span class="sdk-prop-label">Windows Kits Root:</span> <span class="sdk-prop-val">${w.kitsRoot || 'Not detected'}</span></div>
                <div class="sdk-prop-row"><span class="sdk-prop-label">SDK Tools:</span> <span class="sdk-prop-val">${w.tools && w.tools.length > 0 ? w.tools.join(', ') : 'None'}</span></div>
                <div class="sdk-status-msg">${m.statusMessage}</div>
            </div>
        `;
    }

    // Linux GCC Card
    const lCard = document.getElementById('sdk-card-linux');
    if (lCard) {
        const l = report.linux;
        lCard.innerHTML = `
            <div class="sdk-card-header">
                <div class="sdk-title-group">
                    <span class="sdk-icon">🐧</span>
                    <div>
                        <strong class="sdk-name">Linux SDK & GCC / G++ Toolchain</strong>
                        <div class="sdk-version">${l.gccVersion || (l.wslAvailable ? 'WSL2 Subsystem' : 'Host Subsystem')}</div>
                    </div>
                </div>
                <span class="badge ${l.available ? 'badge-success' : 'badge-neutral'}">${l.available ? 'Available' : 'Inactive'}</span>
            </div>
            <div class="sdk-card-body">
                <div class="sdk-prop-row"><span class="sdk-prop-label">Compiler Path:</span> <span class="sdk-prop-val">${l.gccPath || 'N/A'}</span></div>
                <div class="sdk-prop-row"><span class="sdk-prop-label">WSL2 Status:</span> <span class="sdk-prop-val">${l.wslAvailable ? '✅ ' + (l.wslDetails || 'Active') : 'Not installed'}</span></div>
                <div class="sdk-status-msg">${l.statusMessage}</div>
            </div>
        `;
    }

    // Android Card
    const aCard = document.getElementById('sdk-card-android');
    if (aCard) {
        const a = report.android;
        aCard.innerHTML = `
            <div class="sdk-card-header">
                <div class="sdk-title-group">
                    <span class="sdk-icon">🤖</span>
                    <div>
                        <strong class="sdk-name">Android SDK & CLI Toolchain</strong>
                        <div class="sdk-version">CLI ${a.cliVersion || 'N/A'} | ADB Ready</div>
                    </div>
                </div>
                <span class="badge ${a.available ? 'badge-success' : 'badge-warn'}">${a.available ? 'Configured' : 'Missing'}</span>
            </div>
            <div class="sdk-card-body">
                <div class="sdk-prop-row"><span class="sdk-prop-label">Android SDK Path:</span> <span class="sdk-prop-val">${a.sdkPath || 'Not set'}</span></div>
                <div class="sdk-prop-row"><span class="sdk-prop-label">ADB Path:</span> <span class="sdk-prop-val">${a.adbPath ? '✅ ' + a.adbPath : '❌ Missing'}</span></div>
                <div class="sdk-prop-row"><span class="sdk-prop-label">AAPT2 & Signer:</span> <span class="sdk-prop-val">${a.aapt2Path ? '✅ aapt2 present' : '❌ Missing'}</span></div>
                <div class="sdk-prop-row"><span class="sdk-prop-label">NDK Clang:</span> <span class="sdk-prop-val">${a.clangPath ? '✅ NDK Clang available' : '❌ NDK not installed'}</span></div>
                <div class="sdk-status-msg">${a.statusMessage}</div>
            </div>
        `;
    }

    // Populate Custom Path inputs if empty
    populatePathInput('input-custom-vulkan', report.vulkan.sdkPath);
    populatePathInput('input-custom-msvc', report.msvc.installationPath);
    populatePathInput('input-custom-android', report.android.sdkPath);
    populatePathInput('input-custom-ndk', report.android.ndkPath);
}

/**
 * @brief Helper utility to pre-populate an input element value if currently empty.
 * @param {string} id Element ID.
 * @param {string} val Fallback value to populate.
 */
function populatePathInput(id, val) {
    const el = document.getElementById(id);
    if (el && !el.value && val) {
        el.value = val;
    }
}

/**
 * @brief Gathers user-edited SDK directory paths and saves them to project configuration via IPC.
 */
export function saveCustomSdkPaths() {
    const vulkan = document.getElementById('input-custom-vulkan')?.value.trim();
    const msvc = document.getElementById('input-custom-msvc')?.value.trim();
    const android = document.getElementById('input-custom-android')?.value.trim();
    const ndk = document.getElementById('input-custom-ndk')?.value.trim();

    sendIpcMessage('set_sdk_paths', {
        projectRoot: window.activeProjectRoot || '',
        paths: {
            VulkanSdkPath: vulkan || null,
            MsvcPath: msvc || null,
            AndroidSdkPath: android || null,
            AndroidNdkPath: ndk || null
        }
    });
}

/**
 * @brief Renders the list of detected ADB devices into the deployment target dropdown and list cards.
 * @param {Array<Object>} devices List of detected ADB device objects.
 */
function renderAdbDevices(devices) {
    const select = document.getElementById('adb-device-select');
    const container = document.getElementById('adb-devices-list');
    if (!select || !container) return;

    select.innerHTML = '';
    container.innerHTML = '';

    if (!devices || devices.length === 0) {
        select.innerHTML = '<option value="">No ADB devices attached</option>';
        container.innerHTML = '<div style="color:#8b98ad; font-size:11px; padding:6px 0;">No physical devices or emulators connected. Plug in a device with USB Debugging enabled or start an emulator.</div>';
        return;
    }

    devices.forEach(dev => {
        const label = `${dev.model || dev.device || dev.serial} (${dev.state})`;
        const opt = document.createElement('option');
        opt.value = dev.serial;
        opt.textContent = label;
        select.appendChild(opt);

        const card = document.createElement('div');
        card.className = 'adb-dev-item';
        card.innerHTML = `
            <span class="adb-dev-icon">📱</span>
            <div style="flex:1;">
                <strong>${dev.model || dev.serial}</strong>
                <div style="font-size:10px; color:#8b98ad;">Serial: ${dev.serial} | Product: ${dev.product || 'Standard'}</div>
            </div>
            <span class="badge ${dev.state === 'device' ? 'badge-success' : 'badge-warn'}">${dev.state}</span>
        `;
        container.appendChild(card);
    });
}

/**
 * @brief Dispatches an IPC query to refresh connected ADB devices.
 */
export function refreshAdbDevices() {
    sendIpcMessage('get_adb_devices', {});
}

// ==============================================================================
// Native C++20 Compiler
// ==============================================================================

/**
 * @brief Triggers asynchronous C++20 compilation of game native plugins via backend MSVC/Clang.
 */
export function compileNativePlugin() {
    if (isCompiling) return;
    isCompiling = true;

    const btn = document.getElementById('btn-compile-plugin');
    if (btn) {
        btn.disabled = true;
        btn.textContent = '⏳ Compiling...';
    }

    const consoleBox = document.getElementById('compiler-console');
    if (consoleBox) consoleBox.innerHTML = '';

    const targetPlatform = document.getElementById('compiler-target-platform')?.value || 'Win64';
    const optLevel = document.getElementById('compiler-opt-level')?.value || '/O2';
    const avx2 = document.getElementById('compiler-avx2')?.checked ?? true;

    appendCompilerLog(`[START] Initiating C++20 Compilation for target ${targetPlatform}...`);

    sendIpcMessage('compile_native_module', {
        projectRoot: window.activeProjectRoot || '',
        options: {
            TargetPlatform: targetPlatform,
            Optimization: optLevel,
            EnableAvx2: avx2,
            CppStandard: 'c++20',
            OutputName: 'GameNative'
        }
    });
}

/**
 * @brief Appends a formatted line to the compiler output console log.
 * @param {string} line Raw compiler log line.
 */
function appendCompilerLog(line) {
    const consoleBox = document.getElementById('compiler-console');
    if (!consoleBox) return;

    const row = document.createElement('div');
    row.className = 'console-line';

    if (line.includes('error') || line.includes('[ERROR]') || line.includes('[FAILED]')) {
        row.className += ' log-error';
    } else if (line.includes('warning') || line.includes('[WARN]')) {
        row.className += ' log-warn';
    } else if (line.includes('[SUCCESS]')) {
        row.className += ' log-success';
    } else if (line.includes('[INFO]') || line.includes('[EXEC]')) {
        row.className += ' log-info';
    }

    // Check for clickable diagnostic links e.g. File.cpp(14,5)
    const diagMatch = line.match(/^([^(]+)\((\d+)(?:,(\d+))?\):\s*(error|warning)/i);
    if (diagMatch) {
        const file = diagMatch[1];
        const lineNum = diagMatch[2];
        row.innerHTML = `<span class="diag-link" title="Click to view file">${file}:${lineNum}</span> ${escapeHtml(line.slice(diagMatch[0].length))}`;
    } else {
        row.textContent = line;
    }

    consoleBox.appendChild(row);
    consoleBox.scrollTop = consoleBox.scrollHeight;
}

/**
 * @brief Callback invoked when native C++ compilation completes.
 * @param {Object} result Compilation result summary payload.
 */
function onCompileFinished(result) {
    isCompiling = false;
    const btn = document.getElementById('btn-compile-plugin');
    if (btn) {
        btn.disabled = false;
        btn.textContent = '🔨 Compile Native Plugin';
    }

    if (result && result.success) {
        showToast(`✅ Plugin compiled successfully: ${result.outputDllPath}`);
    } else {
        showToast('❌ Compilation failed. Check console output.');
    }
}

// ==============================================================================
// Multi-Platform Packaging & Cooking
// ==============================================================================

/**
 * @brief Starts multi-platform game packaging and asset cooking via backend IPC.
 */
export function packageGame() {
    if (isPackaging) return;
    isPackaging = true;

    const btn = document.getElementById('btn-package-game');
    if (btn) {
        btn.disabled = true;
        btn.textContent = '⏳ Packaging...';
    }

    const consoleBox = document.getElementById('package-console');
    if (consoleBox) consoleBox.innerHTML = '';

    const platform = document.getElementById('package-target-platform')?.value || 'Windows';
    const tier = document.getElementById('package-target-tier')?.value || 'DesktopPC';
    const cookShaders = document.getElementById('package-cook-shaders')?.checked ?? true;
    const bakeMeshes = document.getElementById('package-bake-meshes')?.checked ?? true;

    appendPackageLog(`[START] Packaging game for ${platform} (Quality: ${tier})...`);

    sendIpcMessage('build_project', {
        projectRoot: window.activeProjectRoot || '',
        request: {
            TargetPlatform: platform,
            ScalabilityTier: tier,
            CookShaders: cookShaders,
            BakeGeometryCaches: bakeMeshes,
            PackageSelfContained: true
        }
    });
}

/**
 * @brief Appends a formatted line to the packaging and cooking console log.
 * @param {string} line Raw packaging log line.
 */
function appendPackageLog(line) {
    const consoleBox = document.getElementById('package-console');
    if (!consoleBox) return;

    const row = document.createElement('div');
    row.className = 'console-line';

    if (line.includes('[ERROR]') || line.includes('[FAILED]')) {
        row.className += ' log-error';
    } else if (line.includes('[WARN]')) {
        row.className += ' log-warn';
    } else if (line.includes('[SUCCESS]')) {
        row.className += ' log-success';
    } else if (line.includes('[STEP') || line.includes('[COOK]') || line.includes('[STAGE]')) {
        row.className += ' log-info';
    } else {
        row.textContent = line;
    }

    if (!row.textContent) row.textContent = line;
    consoleBox.appendChild(row);
    consoleBox.scrollTop = consoleBox.scrollHeight;
}

/**
 * @brief Callback invoked when game packaging completes.
 * @param {Object} result Build packaging result payload.
 */
function onPackageFinished(result) {
    isPackaging = false;
    const btn = document.getElementById('btn-package-game');
    if (btn) {
        btn.disabled = false;
        btn.textContent = '📦 Package Game';
    }

    if (result && result.success) {
        showToast(`✅ Game packaged successfully for ${result.targetPlatform}!`);
        const deployBtn = document.getElementById('btn-deploy-android');
        if (deployBtn && result.targetPlatform === 'Android') {
            deployBtn.disabled = false;
        }
    } else {
        showToast('❌ Packaging failed. Check logs.');
    }
}

/**
 * @brief Deploys the packaged Android build onto the selected ADB device or emulator.
 */
export function deployToSelectedDevice() {
    const select = document.getElementById('adb-device-select');
    const deviceId = select ? select.value : null;

    appendPackageLog(`[DEPLOY] Initiating deployment to ${deviceId || 'default device'}...`);

    sendIpcMessage('deploy_to_device', {
        projectRoot: window.activeProjectRoot || '',
        deviceId: deviceId
    });
}

/**
 * @brief Escapes HTML characters in log messages to prevent DOM injection vulnerabilities.
 * @param {string} str Raw string containing potential HTML entities.
 * @returns {string} Sanitized string safe for HTML rendering.
 */
function escapeHtml(str) {
    return str
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}
