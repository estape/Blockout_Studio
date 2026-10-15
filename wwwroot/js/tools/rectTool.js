// ============================================================================
// RECT TOOL (Surface Drawing with 3D Face Reference & Instant Gizmo Positioning)
// ============================================================================

import { scene, groundPlane, raycaster, orbit } from '../engine.js';
import { createBlock, rebuildBlockMesh, selectBlock, blocks, refreshOutliner } from '../sceneManager.js';
import { transformControl, snapSize } from '../transformGizmo.js';
import { recordState } from '../historyManager.js';

let drawStage = 0; // 0: idle, 1: drawing footprint, 2: extruding
let drawStartPt = new THREE.Vector3();
let activeDraftMesh = null;
let pushPullStartMouseY = 0;
let pushPullStartHeight = 0;

function snapVal(val) {
  return Math.round(val / snapSize) * snapSize;
}

export function handleRectPointerDown(e) {
  // If user clicked directly on the TransformControls gizmo, let the gizmo handle it!
  if (transformControl.axis !== null) return;
  if (e.button !== 0) return; // Only left-click creates/interacts

  // Raycast against existing blocks AND the ground plane to find a 3D face reference
  const blockHits = raycaster.intersectObjects(blocks, false);
  let hitPoint = null;

  if (blockHits.length > 0 && blockHits[0].point) {
    hitPoint = blockHits[0].point;
  } else {
    const groundHits = raycaster.intersectObject(groundPlane);
    if (groundHits.length > 0) {
      hitPoint = groundHits[0].point;
    }
  }

  if (!hitPoint) return;

  if (drawStage === 0) {
    // 3D face as reference for start point & gizmo position
    drawStartPt.set(snapVal(hitPoint.x), snapVal(hitPoint.y), snapVal(hitPoint.z));

    activeDraftMesh = createBlock(
      'SM_RectBlock',
      'box',
      Math.max(snapSize, 100),
      Math.max(snapSize, 100),
      Math.max(snapSize, 50),
      drawStartPt.x,
      drawStartPt.z,
      drawStartPt.y,
      false
    );

    // Prominently highlight the object in the scene
    selectBlock(activeDraftMesh);

    // Make gizmo appear directly at the 3D face cursor position
    transformControl.attach(activeDraftMesh);
    transformControl.setSpace('world');

    drawStage = 1;
    // Keep orbit enabled so right-click / middle-click camera control is never lost!
    orbit.enabled = true;

    const liveMeasure = document.getElementById('live-measure');
    if (liveMeasure) {
      liveMeasure.style.display = 'block';
      liveMeasure.textContent = `Rect Spawned at (${drawStartPt.x}, ${drawStartPt.z}) cm — Drag gizmo or footprint to size`;
    }
  } else if (drawStage === 2) {
    // Confirm extruded height
    if (activeDraftMesh) {
      recordState('Create ' + activeDraftMesh.userData.name);
    }
    drawStage = 0;
    activeDraftMesh = null;
    const liveMeasure = document.getElementById('live-measure');
    if (liveMeasure) liveMeasure.style.display = 'none';
  }
}

export function handleRectPointerMove(e) {
  if (!activeDraftMesh) return;

  const liveMeasure = document.getElementById('live-measure');

  if (drawStage === 1 && e.buttons === 1 && transformControl.axis === null) {
    // Dragging footprint across ground plane
    const groundHits = raycaster.intersectObject(groundPlane);
    if (groundHits.length > 0) {
      const p = groundHits[0].point;
      const endX = snapVal(p.x);
      const endZ = snapVal(p.z);
      const w = Math.max(snapSize, Math.abs(endX - drawStartPt.x));
      const l = Math.max(snapSize, Math.abs(endZ - drawStartPt.z));
      const cx = (drawStartPt.x + endX) / 2;
      const cz = (drawStartPt.z + endZ) / 2;

      activeDraftMesh.userData.sizeX = w;
      activeDraftMesh.userData.sizeY = l;
      activeDraftMesh.position.set(cx, activeDraftMesh.position.y, cz);
      rebuildBlockMesh(activeDraftMesh);

      if (liveMeasure) {
        liveMeasure.textContent = `Footprint — W: ${w} cm | L: ${l} cm | H: ${activeDraftMesh.userData.sizeZ} cm`;
      }
    }
  } else if (drawStage === 2) {
    const deltaY = (pushPullStartMouseY - e.clientY) * 2.5;
    const newH = Math.max(snapSize, snapVal(pushPullStartHeight + deltaY));
    activeDraftMesh.userData.sizeZ = newH;
    rebuildBlockMesh(activeDraftMesh);
    if (liveMeasure) {
      liveMeasure.textContent = `Extrude Height — H: ${newH} cm (Click to lock)`;
    }
  }
}

export function handleRectPointerUp(e) {
  if (drawStage === 1 && activeDraftMesh) {
    drawStage = 2;
    pushPullStartMouseY = e.clientY;
    pushPullStartHeight = activeDraftMesh.userData.sizeZ;
  }
}

export function resetRectTool() {
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

