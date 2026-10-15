// ============================================================================
// TRANSFORM GIZMO & 3D IN-SCENE MEASUREMENT BADGE
// ============================================================================

import { scene, camera, renderer, orbit, viewport } from './engine.js';
import { recordState } from './historyManager.js';

export let snapSize = 50;
export const transformControl = new THREE.TransformControls(camera, renderer.domElement);
transformControl.setSpace('world');
transformControl.setTranslationSnap(snapSize);
transformControl.setRotationSnap(THREE.MathUtils.degToRad(15));
scene.add(transformControl);

const badge = document.getElementById('gizmo-measure-badge');

let startPos = new THREE.Vector3();
let startRotY = 0;
let isDraggingGizmo = false;

export function updateSnapSize(newSnap) {
  snapSize = newSnap;
  transformControl.setTranslationSnap(snapSize);
}

transformControl.addEventListener('dragging-changed', (e) => {
  orbit.enabled = !e.value;
  isDraggingGizmo = e.value;

  if (e.value) {
    if (transformControl.object) {
      startPos.copy(transformControl.object.position);
      startRotY = transformControl.object.rotation.y;
    }
    if (badge) badge.style.display = 'block';
  } else {
    if (badge) badge.style.display = 'none';
    if (transformControl.object) {
      const moved = !startPos.equals(transformControl.object.position) || Math.abs(transformControl.object.rotation.y - startRotY) > 0.001;
      if (moved) {
        recordState('Transform ' + transformControl.object.userData.name);
      }
    }
  }
});

transformControl.addEventListener('change', () => {
  if (!isDraggingGizmo || !transformControl.object || !badge) return;

  const obj = transformControl.object;
  const mode = transformControl.getMode();
  const axis = transformControl.axis || '';

  let text = '';

  if (mode === 'translate') {
    let delta = 0;
    let axisLabel = 'Delta';

    if (axis.includes('X')) {
      delta = obj.position.x - startPos.x;
      axisLabel = 'X';
    } else if (axis.includes('Y')) {
      delta = obj.position.y - startPos.y;
      axisLabel = 'Z (Height)';
    } else if (axis.includes('Z')) {
      delta = obj.position.z - startPos.z;
      axisLabel = 'Y (Depth)';
    } else {
      delta = obj.position.distanceTo(startPos);
      axisLabel = 'Dist';
    }

    const sign = delta >= 0 ? '+' : '';
    const absVal = Math.abs(delta);

    // Rule: If |delta| >= 100 cm, format in meters (e.g. 1.00m, 1.50m).
    // If |delta| < 100 cm, format in cm.
    if (absVal >= 100) {
      const meters = (delta / 100).toFixed(2);
      text = `${axisLabel}: ${sign}${meters}m`;
    } else {
      text = `${axisLabel}: ${sign}${Math.round(delta)} cm`;
    }
  } else if (mode === 'rotate') {
    const deltaDeg = Math.round(THREE.MathUtils.radToDeg(obj.rotation.y - startRotY));
    const sign = deltaDeg >= 0 ? '+' : '';
    text = `Rot: ${sign}${deltaDeg}°`;
  }

  badge.textContent = text;

  // Project 3D Gizmo position to 2D Screen Coordinates
  const worldPos = new THREE.Vector3();
  transformControl.object.getWorldPosition(worldPos);
  worldPos.y += 40; // float slightly above object

  const screenPos = worldPos.clone().project(camera);
  const rect = viewport.getBoundingClientRect();
  const x = (screenPos.x * 0.5 + 0.5) * rect.width;
  const y = (-(screenPos.y * 0.5) + 0.5) * rect.height;

  badge.style.left = `${x}px`;
  badge.style.top = `${y}px`;
});

