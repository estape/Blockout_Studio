/**
 * @file transformGizmo.js
 * @brief Three.js TransformControls integration, translation/rotation grid snapping, and on-screen measurement badge.
 */

// ============================================================================
// TRANSFORM GIZMO & 3D IN-SCENE MEASUREMENT BADGE
// ============================================================================

import { scene, camera, renderer, orbit, viewport } from './engine.js';
import { recordState } from './historyManager.js';

/**
 * @type {number}
 * @brief Current grid translation snap distance in centimeters.
 */
export let snapSize = 50;

/**
 * @type {boolean}
 * @brief Whether the transform gizmo is permitted to attach to selected objects.
 */
let transformGizmoEnabled = true;

/**
 * @brief Enables or disables the transform gizmo, detaching it immediately if disabled.
 * @param {boolean} enabled Whether the transform gizmo should be enabled.
 */
export function setTransformGizmoEnabled(enabled) {
  transformGizmoEnabled = enabled;
  if (!enabled) {
    transformControl.detach();
  }
}

/**
 * @brief Checks if the transform gizmo is currently permitted to attach.
 * @returns {boolean} True if enabled.
 */
export function isTransformGizmoEnabled() {
  return transformGizmoEnabled;
}

/**
 * @type {THREE.TransformControls}
 * @brief Three.js transform controls gizmo attached to selected objects.
 */
export const transformControl = new THREE.TransformControls(camera, renderer.domElement);
transformControl.setSpace('world');
transformControl.setTranslationSnap(snapSize);
transformControl.setRotationSnap(THREE.MathUtils.degToRad(15));
scene.add(transformControl);

/**
 * @type {HTMLElement|null}
 * @brief Floating HUD badge displaying transform offsets and distance deltas.
 */
const badge = document.getElementById('gizmo-measure-badge');

/**
 * @type {THREE.Vector3}
 * @brief Cached object position before initiating a gizmo drag operation.
 */
let startPos = new THREE.Vector3();

/**
 * @type {number}
 * @brief Cached object Y-rotation angle in radians before initiating a gizmo drag operation.
 */
let startRotY = 0;

/**
 * @type {boolean}
 * @brief Indicates whether the user is actively dragging the transform gizmo.
 */
let isDraggingGizmo = false;

/**
 * @brief Updates the active grid translation snap distance on the transform gizmo.
 * @param {number} newSnap New translation snap step in centimeters (e.g. 10, 50, 100).
 */
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
