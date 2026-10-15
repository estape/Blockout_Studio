// ============================================================================
// CIRCLE TOOL (Cylindrical Surface & Extrude)
// ============================================================================

import { scene, groundPlane, raycaster, orbit } from '../engine.js';
import { createBlock, rebuildBlockMesh, selectBlock, blocks, refreshOutliner } from '../sceneManager.js';
import { transformControl, snapSize } from '../transformGizmo.js';
import { recordState } from '../historyManager.js';

let drawStage = 0;
let drawStartPt = new THREE.Vector3();
let activeDraftMesh = null;
let pushPullStartMouseY = 0;
let pushPullStartHeight = 0;

function snapVal(val) {
  return Math.round(val / snapSize) * snapSize;
}

export function handleCirclePointerDown(e) {
  if (transformControl.axis !== null || e.button !== 0) return;

  const hits = raycaster.intersectObject(groundPlane);
  if (hits.length === 0) return;

  if (drawStage === 0) {
    const p = hits[0].point;
    drawStartPt.set(snapVal(p.x), 0, snapVal(p.z));
    activeDraftMesh = createBlock('SM_Cylinder', 'cylinder', snapSize * 2, snapSize * 2, snapSize, drawStartPt.x, drawStartPt.z, 0, false);
    selectBlock(activeDraftMesh);
    drawStage = 1;
    orbit.enabled = true;
  } else if (drawStage === 2) {
    if (activeDraftMesh) {
      recordState('Create ' + activeDraftMesh.userData.name);
    }
    drawStage = 0;
    activeDraftMesh = null;
    const liveMeasure = document.getElementById('live-measure');
    if (liveMeasure) liveMeasure.style.display = 'none';
  }
}

export function handleCirclePointerMove(e) {
  if (!activeDraftMesh) return;
  const liveMeasure = document.getElementById('live-measure');

  if (drawStage === 1 && e.buttons === 1) {
    const hits = raycaster.intersectObject(groundPlane);
    if (hits.length > 0) {
      const p = hits[0].point;
      const radius = Math.max(snapSize, snapVal(p.distanceTo(drawStartPt)));
      const diam = radius * 2;
      activeDraftMesh.userData.sizeX = diam;
      activeDraftMesh.userData.sizeY = diam;
      rebuildBlockMesh(activeDraftMesh);
      if (liveMeasure) {
        liveMeasure.style.display = 'block';
        liveMeasure.textContent = `Circle — Radius: ${radius} cm | Diameter: ${diam} cm`;
      }
    }
  } else if (drawStage === 2) {
    const deltaY = (pushPullStartMouseY - e.clientY) * 2.5;
    const newH = Math.max(snapSize, snapVal(pushPullStartHeight + deltaY));
    activeDraftMesh.userData.sizeZ = newH;
    rebuildBlockMesh(activeDraftMesh);
    if (liveMeasure) {
      liveMeasure.textContent = `Cylinder Height — H: ${newH} cm (Click to lock)`;
    }
  }
}

export function handleCirclePointerUp(e) {
  if (drawStage === 1 && activeDraftMesh) {
    drawStage = 2;
    pushPullStartMouseY = e.clientY;
    pushPullStartHeight = activeDraftMesh.userData.sizeZ;
  }
}

export function resetCircleTool() {
  if (activeDraftMesh && drawStage !== 0) {
    scene.remove(activeDraftMesh);
    const idx = blocks.indexOf(activeDraftMesh);
    if (idx > -1) blocks.splice(idx, 1);
    refreshOutliner();
  }
  drawStage = 0;
  activeDraftMesh = null;
  const liveMeasure = document.getElementById('live-measure');
  if (liveMeasure) liveMeasure.style.display = 'none';
}
