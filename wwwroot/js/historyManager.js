// ============================================================================
// HISTORY MANAGER (Undo / Redo Stack & Scene State Management)
// ============================================================================

import { scene } from './engine.js';
import {
  blocks, selectedBlock, selectBlock, refreshOutliner,
  clearSceneBlocks, restoreBlockFromData
} from './sceneManager.js';
import { restoreLightFromData } from './lights.js';
import { showToast } from './ipcBridge.js';

const MAX_HISTORY = 50;

let undoStack = [];
let redoStack = [];
let currentState = null;
let isRestoring = false;

export function isHistoryRestoring() {
  return isRestoring;
}

export function serializeScene() {
  return {
    selectedName: selectedBlock ? selectedBlock.userData.name : null,
    blocks: blocks.map((b) => {
      const u = b.userData;
      return {
        name: u.name,
        shapeType: u.shapeType,
        mobility: u.mobility || 'Static',
        intensity: u.intensity || 5000,
        radius: u.radius || 600,
        color: u.color || '#ffe6a3',
        sizeX: u.sizeX,
        sizeY: u.sizeY,
        sizeZ: u.sizeZ,
        posX: b.position.x,
        posY: b.position.y,
        posZ: b.position.z,
        rotX: b.rotation.x,
        rotY: b.rotation.y,
        rotZ: b.rotation.z,
        customMesh: !!u.customMesh,
        csgData: u.csgData ? JSON.parse(JSON.stringify(u.csgData)) : null,
        aoColors: (b.geometry && b.geometry.attributes.color) ? Array.from(b.geometry.attributes.color.array) : null
      };
    })
  };
}

export function deserializeScene(state) {
  isRestoring = true;
  try {
    clearSceneBlocks();

    state.blocks.forEach((b) => {
      if (b.shapeType === 'point_light' || b.shapeType === 'spot_light') {
        restoreLightFromData(b);
      } else {
        restoreBlockFromData(b);
      }
    });

    if (state.selectedName) {
      const target = blocks.find((b) => b.userData.name === state.selectedName);
      selectBlock(target || null);
    } else {
      selectBlock(null);
    }

    refreshOutliner();
  } finally {
    isRestoring = false;
  }
}

export function initHistory() {
  currentState = serializeScene();
  undoStack = [];
  redoStack = [];
  updateHistoryButtons();
}

export function recordState(label = 'Action') {
  if (isRestoring) return;
  if (!currentState) {
    currentState = serializeScene();
  }
  undoStack.push({ label, state: currentState });
  if (undoStack.length > MAX_HISTORY) {
    undoStack.shift();
  }
  redoStack = [];
  currentState = serializeScene();
  updateHistoryButtons();
}

export function undo() {
  if (undoStack.length === 0) {
    showToast('Nothing to undo');
    return;
  }

  const prev = undoStack.pop();
  redoStack.push({ label: prev.label, state: currentState });
  currentState = prev.state;
  deserializeScene(currentState);
  updateHistoryButtons();
  showToast(`Undo: ${prev.label}`);
}

export function redo() {
  if (redoStack.length === 0) {
    showToast('Nothing to redo');
    return;
  }

  const next = redoStack.pop();
  undoStack.push({ label: next.label, state: currentState });
  currentState = next.state;
  deserializeScene(currentState);
  updateHistoryButtons();
  showToast(`Redo: ${next.label}`);
}

export function updateHistoryButtons() {
  const undoBtn = document.getElementById('btn-undo');
  const redoBtn = document.getElementById('btn-redo');

  if (undoBtn) {
    undoBtn.disabled = undoStack.length === 0;
    if (undoStack.length > 0) {
      const topLabel = undoStack[undoStack.length - 1].label;
      undoBtn.title = `Undo: ${topLabel} (Ctrl+Z)`;
    } else {
      undoBtn.title = 'Undo (Ctrl+Z)';
    }
  }

  if (redoBtn) {
    redoBtn.disabled = redoStack.length === 0;
    if (redoStack.length > 0) {
      const topLabel = redoStack[redoStack.length - 1].label;
      redoBtn.title = `Redo: ${topLabel} (Ctrl+Y / Ctrl+Shift+Z)`;
    } else {
      redoBtn.title = 'Redo (Ctrl+Y / Ctrl+Shift+Z)';
    }
  }
}

