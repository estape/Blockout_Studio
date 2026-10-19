// ============================================================================
// APP ENTRY POINT & EVENT ORCHESTRATOR
// ============================================================================

import {
  scene, camera, renderer, orbit, viewport, getMouseRay, toggleMannequin, mannequinGroup, raycaster,
  setScalabilityTier, toggleGpuHud, updateGpuHud, registerTierChangeCallback, SCALABILITY_TIERS, currentScalabilityTier
} from './engine.js';
import { transformControl, updateSnapSize } from './transformGizmo.js';
import {
  blocks, selectedBlock, selectBlock, createBlock, applyInspectorChanges,
  applyPositionChanges, duplicateSelected, deleteSelected, spawnPreset, refreshOutliner,
  addComponentToSelected, removeComponentFromSelected, updateComponentField, updateScriptProp
} from './sceneManager.js';
import { spawnLight, updateLightShadowLimits } from './lights.js';
import { startPie, pausePie, stopPie, isPieActive, updatePie } from './pieManager.js';
import { handleRectPointerDown, handleRectPointerMove, handleRectPointerUp, resetRectTool } from './tools/rectTool.js';
import { handleCirclePointerDown, handleCirclePointerMove, handleCirclePointerUp, resetCircleTool } from './tools/circleTool.js';
import { handlePushPullPointerDown, handlePushPullPointerMove, handlePushPullPointerUp } from './tools/pushPullTool.js';
import { updateHoleUi, punchHoleOnSelected, applyCsgGeometry } from './tools/holeTool.js';
import {
  copyToClipboard, saveProject, loadProject, loadProjectFromData, requestBakeAo,
  applyAoColors, exportToObj, exportToUsd, queryToolchains, queryNativeStatus
} from './sceneBridge.js';
import { registerHandler, showToast } from './ipcBridge.js';
export { showToast };
import { initHistory, undo, redo, recordState } from './historyManager.js';
import {
  initAssetManager, toggleContentDrawer, togglePinDrawer, selectFolder,
  selectCollection, setAssetTypeFilter, selectAssetTile, handleAssetDoubleClick,
  handleAssetContextMenu, hideContextMenu, cmPlaceInScene, cmReimport,
  cmShowInExplorer, cmCopyVirtualPath, cmDelete, cmOpenReferenceViewer,
  openReferenceViewer, openReferenceViewerByPath, closeReferenceViewer,
  importAssetDialog, createProjectDialog, openProjectDialog
} from './assetManager.js';
import { initUsdObjLoader, saveSelectedAsObj, saveLevelAsUsdStage } from './usdObjLoader.js';
import {
  initBuildManager, openBuildModal, closeBuildModal, switchBuildTab,
  saveCustomSdkPaths, refreshAdbDevices, compileNativePlugin, packageGame,
  deployToSelectedDevice
} from './buildManager.js';

let currentTool = 'select';

export function setTool(tool) {
  currentTool = tool;

  ['select', 'rotate', 'draw-rect', 'draw-circle', 'pushpull', 'hole'].forEach((t) => {
    const btn = document.getElementById('tool-' + t);
    if (btn) btn.classList.toggle('active', t === tool);
  });

  const holeOpts = document.getElementById('hole-options');
  if (holeOpts) holeOpts.style.display = (tool === 'hole') ? 'block' : 'none';

  resetRectTool();
  resetCircleTool();

  if (tool === 'select') {
    transformControl.setMode('translate');
    transformControl.setSpace('world');
    if (selectedBlock) transformControl.attach(selectedBlock);
  } else if (tool === 'rotate') {
    transformControl.setMode('rotate');
    transformControl.setSpace('world');
    if (selectedBlock) transformControl.attach(selectedBlock);
  } else {
    transformControl.detach();
  }
}

// Global Pointer Event Listeners
renderer.domElement.addEventListener('pointerdown', (e) => {
  getMouseRay(e);

  if (currentTool === 'select' || currentTool === 'rotate' || currentTool === 'hole') {
    if (transformControl.axis === null && e.button === 0) {
      if (mannequinGroup && mannequinGroup.visible) {
        const dummyHits = raycaster.intersectObjects(mannequinGroup.children, true);
        if (dummyHits.length > 0) {
          selectBlock(mannequinGroup);
          return;
        }
      }
      const hits = raycasterHits();
      if (hits.length > 0) {
        selectBlock(hits[0].object);
      }
    }
  } else if (currentTool === 'draw-rect') {
    handleRectPointerDown(e);
  } else if (currentTool === 'draw-circle') {
    handleCirclePointerDown(e);
  } else if (currentTool === 'pushpull') {
    handlePushPullPointerDown(e);
  }
});

renderer.domElement.addEventListener('pointermove', (e) => {
  getMouseRay(e);

  if (currentTool === 'draw-rect') {
    handleRectPointerMove(e);
  } else if (currentTool === 'draw-circle') {
    handleCirclePointerMove(e);
  } else if (currentTool === 'pushpull') {
    handlePushPullPointerMove(e);
  }
});

window.addEventListener('pointerup', (e) => {
  if (currentTool === 'draw-rect') {
    handleRectPointerUp(e);
  } else if (currentTool === 'draw-circle') {
    handleCirclePointerUp(e);
  } else if (currentTool === 'pushpull') {
    handlePushPullPointerUp();
  }
});

function raycasterHits() {
  return raycaster.intersectObjects(blocks, false);
}

// Keyboard Shortcuts
window.addEventListener('keydown', (e) => {
  if (document.activeElement.tagName === 'INPUT' || document.activeElement.tagName === 'SELECT') return;

  // Toggle Content Drawer: Ctrl + Space
  if ((e.ctrlKey || e.metaKey) && (e.code === 'Space' || e.key === ' ')) {
    e.preventDefault();
    toggleContentDrawer();
    return;
  }

  // History Shortcuts: Ctrl+Z (Undo), Ctrl+Y or Ctrl+Shift+Z (Redo)
  if ((e.ctrlKey || e.metaKey) && (e.key === 'z' || e.key === 'Z')) {
    e.preventDefault();
    if (e.shiftKey) {
      redo();
    } else {
      undo();
    }
    return;
  }
  if ((e.ctrlKey || e.metaKey) && (e.key === 'y' || e.key === 'Y')) {
    e.preventDefault();
    redo();
    return;
  }

  if (e.key === 'Delete') deleteSelected();

  if ((e.ctrlKey || e.metaKey) && (e.key === 'd' || e.key === 'D')) {
    e.preventDefault();
    duplicateSelected();
  }
  if ((e.ctrlKey || e.metaKey) && (e.key === 's' || e.key === 'S')) {
    e.preventDefault();
    saveProject();
    return;
  }
  if ((e.ctrlKey || e.metaKey) && (e.key === 'o' || e.key === 'O')) {
    e.preventDefault();
    if (e.shiftKey) {
      openProjectDialog();
    } else {
      loadProject();
    }
    return;
  }
  if ((e.ctrlKey || e.metaKey) && (e.key === 'n' || e.key === 'N')) {
    e.preventDefault();
    createProjectDialog();
    return;
  }
  if ((e.ctrlKey || e.metaKey) && (e.key === 'b' || e.key === 'B')) {
    e.preventDefault();
    openBuildModal();
  }
  if (e.key === 'F7') {
    e.preventDefault();
    openBuildModal();
  }
});

function spawnSpherePrompt() {
  const diam = prompt('Enter Sphere Diameter in cm:', '100');
  if (diam) {
    const d = Math.max(10, parseFloat(diam) || 100);
    createBlock('SM_Sphere', 'sphere', d, d, d, 0, 0, 0);
  }
}

// Viewport & Window Resize Handling (ResizeObserver handles toolbar wrap & drawer docking)
function syncViewportSize() {
  const w = Math.max(1, viewport.clientWidth);
  const h = Math.max(1, viewport.clientHeight);
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
  renderer.setSize(w, h);
}

window.addEventListener('resize', syncViewportSize);
if (typeof ResizeObserver !== 'undefined') {
  const viewportResizeObserver = new ResizeObserver(() => {
    syncViewportSize();
  });
  viewportResizeObserver.observe(viewport);
}

// Top Menu Bar Popover Orchestration & Anchor Fallback
function initMenuBarPopovers() {
  const supportsAnchorPositioning = 'anchorName' in document.documentElement.style;
  const triggers = document.querySelectorAll('.menubar-trigger[popovertarget]');
  const popovers = document.querySelectorAll('.menubar-popover[popover]');

  popovers.forEach((popover) => {
    // Auto-close popover when an action button inside it is clicked
    popover.addEventListener('click', (e) => {
      const item = e.target.closest('.menu-action-item');
      if (item && typeof popover.hidePopover === 'function') {
        popover.hidePopover();
      }
    });

    // Coordinate fallback for engines without CSS Anchor Positioning
    if (!supportsAnchorPositioning) {
      popover.addEventListener('beforetoggle', (e) => {
        if (e.newState === 'open') {
          const trigger = document.querySelector(`[popovertarget="${popover.id}"]`);
          if (trigger) {
            const rect = trigger.getBoundingClientRect();
            popover.style.position = 'fixed';
            popover.style.margin = '0';
            popover.style.top = `${rect.bottom + 2}px`;
            popover.style.left = `${rect.left}px`;
          }
        }
      });
    }
  });

  // Classic desktop menu bar behavior: hovering another trigger while a menu is open switches menus
  triggers.forEach((trigger) => {
    trigger.addEventListener('pointerenter', () => {
      const openMenu = document.querySelector('.menubar-popover:popover-open');
      const targetId = trigger.getAttribute('popovertarget');
      const targetPopover = targetId ? document.getElementById(targetId) : null;
      if (openMenu && targetPopover && openMenu !== targetPopover && typeof targetPopover.showPopover === 'function') {
        targetPopover.showPopover();
      }
    });
  });
}
initMenuBarPopovers();

// ============================================================================
// RESIZABLE COLUMNS (Left Sidebar & Right Inspector)
// ============================================================================
function initColumnResizers() {
  const leftResizer = document.getElementById('resizer-left');
  const rightResizer = document.getElementById('resizer-right');
  const workspace = document.getElementById('workspace');
  if (!workspace || !leftResizer || !rightResizer) return;

  // Restore saved column widths if available
  const savedLeft = localStorage.getItem('blockout_left_width');
  const savedRight = localStorage.getItem('blockout_right_width');
  if (savedLeft) document.documentElement.style.setProperty('--left-sidebar-width', `${Math.max(160, Math.min(450, parseInt(savedLeft, 10)))}px`);
  if (savedRight) document.documentElement.style.setProperty('--sidebar-width', `${Math.max(220, Math.min(550, parseInt(savedRight, 10)))}px`);

  let isDraggingLeft = false;
  let isDraggingRight = false;

  leftResizer.addEventListener('pointerdown', (e) => {
    isDraggingLeft = true;
    leftResizer.classList.add('dragging');
    leftResizer.setPointerCapture(e.pointerId);
    document.body.style.userSelect = 'none';
    document.body.style.cursor = 'col-resize';
  });

  rightResizer.addEventListener('pointerdown', (e) => {
    isDraggingRight = true;
    rightResizer.classList.add('dragging');
    rightResizer.setPointerCapture(e.pointerId);
    document.body.style.userSelect = 'none';
    document.body.style.cursor = 'col-resize';
  });

  window.addEventListener('pointermove', (e) => {
    if (isDraggingLeft) {
      const workspaceRect = workspace.getBoundingClientRect();
      const newWidth = Math.max(160, Math.min(450, e.clientX - workspaceRect.left));
      document.documentElement.style.setProperty('--left-sidebar-width', `${newWidth}px`);
      localStorage.setItem('blockout_left_width', newWidth);
    } else if (isDraggingRight) {
      const workspaceRect = workspace.getBoundingClientRect();
      const newWidth = Math.max(220, Math.min(550, workspaceRect.right - e.clientX));
      document.documentElement.style.setProperty('--sidebar-width', `${newWidth}px`);
      localStorage.setItem('blockout_right_width', newWidth);
    }
  });

  window.addEventListener('pointerup', (e) => {
    if (isDraggingLeft) {
      isDraggingLeft = false;
      leftResizer.classList.remove('dragging');
      document.body.style.userSelect = '';
      document.body.style.cursor = '';
    }
    if (isDraggingRight) {
      isDraggingRight = false;
      rightResizer.classList.remove('dragging');
      document.body.style.userSelect = '';
      document.body.style.cursor = '';
    }
  });
}
initColumnResizers();

// ============================================================================
// WELCOME SCREEN MODAL
// ============================================================================
export function openWelcomeModal() {
  const modal = document.getElementById('welcome-modal');
  if (modal) modal.style.display = 'flex';
}

export function closeWelcomeModal() {
  const modal = document.getElementById('welcome-modal');
  if (modal) modal.style.display = 'none';
}

export function welcomeNewProject() {
  closeWelcomeModal();
  createProjectDialog();
}

export function welcomeOpenProject() {
  closeWelcomeModal();
  openProjectDialog();
}

export function welcomeQuickStart() {
  closeWelcomeModal();
}

export function toggleWelcomeStartup(checked) {
  localStorage.setItem('blockout_show_welcome', checked ? 'true' : 'false');
}

function initWelcomeModal() {
  const show = localStorage.getItem('blockout_show_welcome');
  const checkbox = document.getElementById('welcome-show-startup');
  if (checkbox) checkbox.checked = (show !== 'false');

  if (show !== 'false') {
    setTimeout(() => {
      openWelcomeModal();
    }, 150);
  }
}
initWelcomeModal();

// Render Loop & PIE Runtime
const clock = new THREE.Clock();

function animate() {
  requestAnimationFrame(animate);
  const dt = clock.getDelta();

  if (isPieActive()) {
    updatePie(dt);
  } else {
    orbit.update();
  }

  renderer.render(scene, camera);
  updateGpuHud();
}
animate();

// Initialize Scalability Tier Hook
registerTierChangeCallback((tier) => {
  updateLightShadowLimits(tier.maxShadowLights);
  showToast(`Active Scalability: ${tier.shortName}`, false);
});

// Register IPC Response Handlers
registerHandler('copy_clipboard_response', (msg) => {
  showToast(msg.message, !msg.success);
});

registerHandler('save_project_response', (msg) => {
  if (msg.success) showToast('Project saved: ' + msg.path);
});

registerHandler('load_project_response', (msg) => {
  if (msg.success && msg.content) {
    loadProjectFromData(JSON.parse(msg.content));
    showToast('Project loaded from: ' + msg.path);
  }
});

registerHandler('csg_cut_response', (msg) => {
  if (msg.success && selectedBlock) {
    applyCsgGeometry(selectedBlock, msg.positions, msg.normals, msg.uvs, msg.indices);
    recordState('Hole Cut ' + selectedBlock.userData.name);
    showToast('Hole cut successfully via C# CSG!');
  }
});

registerHandler('bake_ao_response', (msg) => {
  if (msg.success && msg.colors) {
    applyAoColors(msg.targetId, msg.colors);
    const mode = msg.accelerationMode || 'C# SIMD Multi-Core';
    const elapsed = msg.elapsedMs ? ` (${msg.elapsedMs.toFixed(1)} ms)` : '';
    showToast(`AO baked via ${mode}${elapsed}!`);
  }
});

registerHandler('scalability_tiers_response', (msg) => {
  if (msg.success && msg.activeTierIndex !== undefined) {
    setScalabilityTier(msg.activeTierIndex, false);
  }
});

registerHandler('compile_shader_response', (msg) => {
  if (msg.success) {
    showToast(`Shader compiled (${msg.stage}) -> SPIR-V & GLSL ES 3.00 (${msg.elapsedMs ? msg.elapsedMs.toFixed(1) : 0} ms)!`);
  } else {
    showToast(`Shader compilation error: ${msg.error}`, true);
  }
});

registerHandler('export_usd_response', (msg) => {
  if (msg.success) {
    showToast('Exported USD Stage: ' + msg.path);
  } else {
    showToast('USD Export cancelled or failed', true);
  }
});

let latestToolchainReport = null;
let latestNativeStatus = null;

registerHandler('toolchains_response', (msg) => {
  if (msg.report) {
    latestToolchainReport = msg.report;
    const badge = document.getElementById('angle-badge');
    if (badge) {
      const backend = msg.report.activeWebViewAngleBackend || 'Vulkan';
      badge.textContent = backend;
      if (backend.toLowerCase().includes('vulkan')) {
        badge.style.background = '#0078d4';
      } else {
        badge.style.background = '#6b7280';
      }
    }
    const modal = document.getElementById('toolchain-modal');
    if (modal && modal.style.display === 'flex') {
      renderToolchainsModal();
    }
  }
});

registerHandler('native_status_response', (msg) => {
  latestNativeStatus = msg;
  const modal = document.getElementById('toolchain-modal');
  if (modal && modal.style.display === 'flex') {
    renderToolchainsModal();
  }
});

function renderToolchainsModal() {
  const body = document.getElementById('toolchain-modal-body');
  if (!body) return;

  if (!latestToolchainReport) {
    body.innerHTML = '<div style="padding:15px; color:#8b98ad; text-align:center;">Probing system toolchains & graphics runtimes...</div>';
    return;
  }

  const r = latestToolchainReport;
  const n = latestNativeStatus;

  const badgeHtml = (ok, text = null) => ok
    ? `<span style="background:#1b4729; color:#4ade80; padding:2px 6px; border-radius:3px; font-weight:600; font-size:11px;">✓ ${text || 'Available'}</span>`
    : `<span style="background:#4a1b1b; color:#f87171; padding:2px 6px; border-radius:3px; font-weight:600; font-size:11px;">✗ ${text || 'Not Found'}</span>`;

  let html = '';

  // Host & Graphics Runtime
  html += `
    <div style="background:#15181e; border:1px solid #2d3748; border-radius:6px; padding:10px; margin-bottom:10px;">
      <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:6px;">
        <strong style="color:#f58220;">Graphics & Host Runtime</strong>
        ${badgeHtml(true, r.activeWebViewAngleBackend || 'Vulkan ANGLE')}
      </div>
      <div style="color:#cbd5e1; font-size:11px;">
        <div><b>OS:</b> ${r.hostOs || 'Unknown'}</div>
        <div><b>ANGLE Backend:</b> ${r.activeWebViewAngleBackend || 'Vulkan'}</div>
        <div><b>Viewport Interface:</b> WebGL 2.0 (Unified Editor / PIE / Scalability)</div>
      </div>
    </div>
  `;

  // Native C++20 Core
  const nativeLoaded = n?.isLoaded ?? false;
  html += `
    <div style="background:#15181e; border:1px solid #2d3748; border-radius:6px; padding:10px; margin-bottom:10px;">
      <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:6px;">
        <strong style="color:#60a5fa;">Native C++20 Core (EngineNative)</strong>
        ${badgeHtml(nativeLoaded, nativeLoaded ? 'Native C++20' : 'Managed SIMD Fallback')}
      </div>
      <div style="color:#cbd5e1; font-size:11px;">
        <div><b>Mode:</b> ${nativeLoaded ? `C++20 Shared Library (v${n.version}) via [LibraryImport]` : 'C# Hardware Intrinsics (System.Runtime.Intrinsics)'}</div>
        <div><b>SIMD Capabilities:</b> ${n?.simdCaps || 'Detecting...'}</div>
        <div style="color:#94a3b8; font-size:10px; margin-top:2px;">${n?.description || ''}</div>
      </div>
    </div>
  `;

  // Vulkan SDK
  html += `
    <div style="background:#15181e; border:1px solid #2d3748; border-radius:6px; padding:10px; margin-bottom:10px;">
      <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:6px;">
        <strong style="color:#f43f5e;">Vulkan SDK & Shader Compilers</strong>
        ${badgeHtml(r.vulkan.isAvailable, r.vulkan.version || (r.vulkan.isAvailable ? 'Installed' : 'Missing'))}
      </div>
      <div style="color:#cbd5e1; font-size:11px;">
        <div><b>Path:</b> ${r.vulkan.path || 'Not detected in VULKAN_SDK or PATH'}</div>
        <div><b>Tools Detected:</b> ${r.vulkan.installedTools?.length ? r.vulkan.installedTools.join(', ') : 'None'}</div>
        <div style="color:#94a3b8; font-size:10px; margin-top:2px;">${r.vulkan.details}</div>
      </div>
    </div>
  `;

  // MSVC / Windows SDK
  html += `
    <div style="background:#15181e; border:1px solid #2d3748; border-radius:6px; padding:10px; margin-bottom:10px;">
      <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:6px;">
        <strong style="color:#38bdf8;">Windows SDK & MSVC (vswhere / cl.exe)</strong>
        ${badgeHtml(r.msvc.isAvailable, r.msvc.version || (r.msvc.isAvailable ? 'Installed' : 'Missing'))}
      </div>
      <div style="color:#cbd5e1; font-size:11px;">
        <div><b>Installation:</b> ${r.msvc.path || 'Not found'}</div>
        <div><b>Tools Detected:</b> ${r.msvc.installedTools?.length ? r.msvc.installedTools.join(', ') : 'None'}</div>
        <div style="color:#94a3b8; font-size:10px; margin-top:2px;">${r.msvc.details}</div>
      </div>
    </div>
  `;

  // Android SDK & NDK
  html += `
    <div style="background:#15181e; border:1px solid #2d3748; border-radius:6px; padding:10px; margin-bottom:10px;">
      <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:6px;">
        <strong style="color:#34d399;">Android SDK / NDK Toolchain</strong>
        ${badgeHtml(r.android.isAvailable, r.android.version || (r.android.isAvailable ? 'Configured' : 'Not Found'))}
      </div>
      <div style="color:#cbd5e1; font-size:11px;">
        <div><b>Location:</b> ${r.android.path || 'Not found'}</div>
        <div><b>Tools Detected:</b> ${r.android.installedTools?.length ? r.android.installedTools.join(', ') : 'None'}</div>
        <div style="color:#94a3b8; font-size:10px; margin-top:2px;">${r.android.details}</div>
      </div>
    </div>
  `;

  // Linux GCC / Clang
  html += `
    <div style="background:#15181e; border:1px solid #2d3748; border-radius:6px; padding:10px;">
      <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:6px;">
        <strong style="color:#a78bfa;">Linux GCC / G++ Toolchain</strong>
        ${badgeHtml(r.gcc.isAvailable, r.gcc.version || (r.gcc.isAvailable ? 'Installed' : 'Not Found'))}
      </div>
      <div style="color:#cbd5e1; font-size:11px;">
        <div><b>Path:</b> ${r.gcc.path || 'Not detected'}</div>
        <div style="color:#94a3b8; font-size:10px; margin-top:2px;">${r.gcc.details}</div>
      </div>
    </div>
  `;

  body.innerHTML = html;
}

export function openToolchainsModal() {
  const modal = document.getElementById('toolchain-modal');
  if (modal) modal.style.display = 'flex';
  renderToolchainsModal();
  queryToolchains();
  queryNativeStatus();
}

export function closeToolchainsModal() {
  const modal = document.getElementById('toolchain-modal');
  if (modal) modal.style.display = 'none';
}

registerHandler('error', (msg) => {
  showToast('Error: ' + msg.message, true);
});

// Seed Default Starter Scene
createBlock('SM_Floor', 'box', 600, 600, 20, 0, 0, 0, false);
createBlock('SM_Wall_Back', 'box', 600, 20, 300, 0, -290, 20, false);
createBlock('SM_Ramp_Access', 'ramp', 150, 300, 100, 150, 0, 20, false);
createBlock('SM_CoverPillar', 'cylinder', 100, 100, 200, -180, 0, 20, false);
spawnLight('point', 'Stationary', false);

// Initialize Baseline State
initHistory();

// Probe toolchains and native acceleration status on startup
queryToolchains();
queryNativeStatus();

// Initialize Asset Manager & Content Drawer
initAssetManager();

// Initialize Native .OBJ & .USD Geometry Streaming Loader
initUsdObjLoader();

// Initialize Direct Native SDK Manager & Build Pipeline (Module 5)
initBuildManager();

// Expose functions to window for HTML UI buttons
window.setTool = setTool;
window.spawnPreset = spawnPreset;
window.spawnLight = spawnLight;
window.spawnSpherePrompt = spawnSpherePrompt;
window.toggleMannequin = toggleMannequin;
window.undo = undo;
window.redo = redo;
window.updateSnapSize = () => {
  const el = document.getElementById('snap-size');
  if (el) updateSnapSize(parseInt(el.value, 10));
};
window.applyInspectorChanges = applyInspectorChanges;
window.applyPositionChanges = applyPositionChanges;
window.duplicateSelected = duplicateSelected;
window.deleteSelected = deleteSelected;
window.updateHoleUi = updateHoleUi;
window.punchHoleOnSelected = punchHoleOnSelected;
window.copyToClipboard = copyToClipboard;
window.saveProject = saveProject;
window.loadProject = loadProject;
window.loadProjectFromData = loadProjectFromData;
window.exportToObj = exportToObj;
window.exportToUsd = exportToUsd;
window.requestBakeAo = requestBakeAo;
window.openToolchainsModal = openToolchainsModal;
window.closeToolchainsModal = closeToolchainsModal;
window.queryToolchains = queryToolchains;

// Native .OBJ & .USD Pipeline Actions (Module 2)
window.saveSelectedAsObj = saveSelectedAsObj;
window.saveLevelAsUsdStage = saveLevelAsUsdStage;

// Content Drawer & Asset Manager Window Bindings
window.toggleContentDrawer = toggleContentDrawer;
window.togglePinDrawer = togglePinDrawer;
window.selectFolder = selectFolder;
window.selectCollection = selectCollection;
window.setAssetTypeFilter = setAssetTypeFilter;
window.selectAssetTile = selectAssetTile;
window.handleAssetDoubleClick = handleAssetDoubleClick;
window.handleAssetContextMenu = handleAssetContextMenu;
window.hideContextMenu = hideContextMenu;
window.cmPlaceInScene = cmPlaceInScene;
window.cmReimport = cmReimport;
window.cmShowInExplorer = cmShowInExplorer;
window.cmCopyVirtualPath = cmCopyVirtualPath;
window.cmDelete = cmDelete;
window.cmOpenReferenceViewer = cmOpenReferenceViewer;
window.openReferenceViewer = openReferenceViewer;
window.openReferenceViewerByPath = openReferenceViewerByPath;
window.closeReferenceViewer = closeReferenceViewer;
window.importAssetDialog = importAssetDialog;
window.createProjectDialog = createProjectDialog;
window.openProjectDialog = openProjectDialog;

// Scalability & Performance HUD Window Bindings
window.setScalabilityTier = setScalabilityTier;
window.toggleGpuHud = toggleGpuHud;

// PIE & ECS Window Bindings (Module 4)
window.startPie = startPie;
window.pausePie = pausePie;
window.stopPie = stopPie;
window.onAddComponentSelected = addComponentToSelected;
window.removeComponentFromSelected = removeComponentFromSelected;
window.updateComponentField = updateComponentField;
window.updateScriptProp = updateScriptProp;

// Build, Compilers & SDK Settings Window Bindings (Module 5)
window.openBuildModal = openBuildModal;
window.closeBuildModal = closeBuildModal;
window.switchBuildTab = switchBuildTab;
window.saveCustomSdkPaths = saveCustomSdkPaths;
window.refreshAdbDevices = refreshAdbDevices;
window.compileNativePlugin = compileNativePlugin;
window.packageGame = packageGame;
window.deployToSelectedDevice = deployToSelectedDevice;

// Welcome Screen Window Bindings
window.openWelcomeModal = openWelcomeModal;
window.closeWelcomeModal = closeWelcomeModal;
window.welcomeNewProject = welcomeNewProject;
window.welcomeOpenProject = welcomeOpenProject;
window.welcomeQuickStart = welcomeQuickStart;
window.toggleWelcomeStartup = toggleWelcomeStartup;


