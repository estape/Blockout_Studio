/**
 * @file pushPullTool.js
 * @brief Push/Pull face extrusion tool for dynamically resizing mesh dimensions along face normals.
 */

import { raycaster, orbit } from '../engine.js';
import { selectedBlock, selectBlock, rebuildBlockMesh, syncInspectorFromSelected, blocks } from '../sceneManager.js';
import { snapSize } from '../transformGizmo.js';
import { recordState } from '../historyManager.js';

/** @type {boolean} Flag indicating whether an active drag operation is in progress. */
let isPushPulling = false;

/** @type {THREE.Vector3} World-space surface normal of the intersected face. */
let pushPullFaceNormal = new THREE.Vector3(0, 1, 0);

/** @type {{x: number, y: number}} Initial mouse screen coordinates at pointer down. */
let pushPullStartMouse = { x: 0, y: 0 };

/** @type {{x: number, y: number, z: number}} Block dimensions prior to extrusion drag. */
let pushPullInitialSizes = { x: 100, y: 100, z: 100 };

/**
 * Snaps scalar values to the active grid step.
 * @param {number} val - Input distance scalar.
 * @returns {number} Grid-aligned distance scalar.
 */
function snapVal(val) {
  return Math.round(val / snapSize) * snapSize;
}

/**
 * Initiates face extrusion upon clicking an existing block surface.
 * @param {PointerEvent|MouseEvent} e - Pointer event.
 */
export function handlePushPullPointerDown(e) {
  if (e.button !== 0) return;

  const hits = raycaster.intersectObjects(blocks, false);
  if (hits.length > 0) {
    selectBlock(hits[0].object);
    isPushPulling = true;
    pushPullStartMouse = { x: e.clientX, y: e.clientY };
    pushPullInitialSizes = {
      x: selectedBlock.userData.sizeX,
      y: selectedBlock.userData.sizeY,
      z: selectedBlock.userData.sizeZ
    };

    if (hits[0].face) {
      pushPullFaceNormal.copy(hits[0].face.normal).transformDirection(selectedBlock.matrixWorld);
    }

    orbit.enabled = false;
    const liveMeasure = document.getElementById('live-measure');
    if (liveMeasure) liveMeasure.style.display = 'block';
  }
}

/**
 * Updates selected block dimensions based on pointer delta projected onto the face normal.
 * @param {PointerEvent|MouseEvent} e - Pointer event.
 */
export function handlePushPullPointerMove(e) {
  if (!isPushPulling || !selectedBlock) return;

  const deltaY = (pushPullStartMouse.y - e.clientY) * 2.0;
  const deltaX = (e.clientX - pushPullStartMouse.x) * 2.0;
  const liveMeasure = document.getElementById('live-measure');

  // Push/Pull along face normal
  if (Math.abs(pushPullFaceNormal.y) > 0.5) {
    const newZ = Math.max(snapSize, snapVal(pushPullInitialSizes.z + deltaY));
    selectedBlock.userData.sizeZ = newZ;
    if (liveMeasure) liveMeasure.textContent = `Push/Pull Height (Z): ${newZ} cm`;
  } else if (Math.abs(pushPullFaceNormal.x) > 0.5) {
    const newX = Math.max(snapSize, snapVal(pushPullInitialSizes.x + deltaX));
    selectedBlock.userData.sizeX = newX;
    if (liveMeasure) liveMeasure.textContent = `Push/Pull Width (X): ${newX} cm`;
  } else {
    const newY = Math.max(snapSize, snapVal(pushPullInitialSizes.y + deltaY));
    selectedBlock.userData.sizeY = newY;
    if (liveMeasure) liveMeasure.textContent = `Push/Pull Length (Y): ${newY} cm`;
  }

  rebuildBlockMesh(selectedBlock);
  syncInspectorFromSelected();
}

/**
 * Commits the extrusion operation and registers an undo/redo history snapshot.
 */
export function handlePushPullPointerUp() {
  if (isPushPulling) {
    isPushPulling = false;
    orbit.enabled = true;
    const liveMeasure = document.getElementById('live-measure');
    if (liveMeasure) liveMeasure.style.display = 'none';

    if (selectedBlock) {
      const u = selectedBlock.userData;
      if (u.sizeX !== pushPullInitialSizes.x || u.sizeY !== pushPullInitialSizes.y || u.sizeZ !== pushPullInitialSizes.z) {
        recordState('Push/Pull ' + u.name);
      }
    }
  }
}
