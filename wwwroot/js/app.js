// ============================================================================
// APP ENTRY POINT & EVENT ORCHESTRATOR
// ============================================================================

import { scene, camera, renderer, orbit, viewport, getMouseRay, toggleMannequin, raycaster } from './engine.js';
import { transformControl, updateSnapSize } from './transformGizmo.js';
import {
  blocks, selectedBlock, selectBlock, createBlock, applyInspectorChanges,
  applyPositionChanges, duplicateSelected, deleteSelected, spawnPreset, refreshOutliner
} from './sceneManager.js';
import { spawnLight } from './lights.js';
import { handleRectPointerDown, handleRectPointerMove, handleRectPointerUp, resetRectTool } from './tools/rectTool.js';
import { handleCirclePointerDown, handleCirclePointerMove, handleCirclePointerUp, resetCircleTool } from './tools/circleTool.js';
import { handlePushPullPointerDown, handlePushPullPointerMove, handlePushPullPointerUp } from './tools/pushPullTool.js';
import { updateHoleUi, punchHoleOnSelected, applyCsgGeometry } from './tools/holeTool.js';
import { copyToUe5, saveProject, loadProject, loadProjectFromData, requestBakeAo, applyAoColors, exportToUE5Obj } from './ue5Bridge.js';
import { registerHandler, showToast } from './ipcBridge.js';
import { initHistory, undo, redo, recordState } from './historyManager.js';

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

  if (e.key === 'w' || e.key === 'W' || e.key === 'v' || e.key === 'V') setTool('select');
  if (e.key === 'e' || e.key === 'E') setTool('rotate');
  if (e.key === 'r' || e.key === 'R') setTool('draw-rect');
  if (e.key === 'c' || e.key === 'C') setTool('draw-circle');
  if (e.key === 'p' || e.key === 'P') setTool('pushpull');
  if (e.key === 'h' || e.key === 'H') setTool('hole');
  if (e.key === 's' || e.key === 'S') spawnSpherePrompt();
  if (e.key === 'l' || e.key === 'L') spawnLight('point', 'Static');
  if (e.key === 'Delete') deleteSelected();

  if ((e.ctrlKey || e.metaKey) && (e.key === 'd' || e.key === 'D')) {
    e.preventDefault();
    duplicateSelected();
  }
  if ((e.ctrlKey || e.metaKey) && (e.key === 'c' || e.key === 'C')) {
    e.preventDefault();
    copyToUe5();
  }
});

function spawnSpherePrompt() {
  const diam = prompt('Enter Sphere Diameter in cm (UE5 units):', '100');
  if (diam) {
    const d = Math.max(10, parseFloat(diam) || 100);
    createBlock('SM_Sphere', 'sphere', d, d, d, 0, 0, 0);
  }
}

// Window Resize
window.addEventListener('resize', () => {
  camera.aspect = viewport.clientWidth / viewport.clientHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(viewport.clientWidth, viewport.clientHeight);
});

// Render Loop
function animate() {
  requestAnimationFrame(animate);
  orbit.update();
  renderer.render(scene, camera);
}
animate();

// Register IPC Response Handlers
registerHandler('copy_ue5_response', (msg) => {
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
    showToast('AO contact shadows baked via C# (.NET 10 SIMD)!');
  }
});

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
window.copyToUe5 = copyToUe5;
window.saveProject = saveProject;
window.loadProject = loadProject;
window.exportToUE5Obj = exportToUE5Obj;
window.requestBakeAo = requestBakeAo;
