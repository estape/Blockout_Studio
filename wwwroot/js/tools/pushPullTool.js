/**
 * @file pushPullTool.js
 * @brief Push/Pull single-face extrusion tool featuring Cyan Blueprint face highlighting and normal-axis gizmo arrow.
 */

// ============================================================================
// PUSH/PULL SINGLE-FACE EXTRUSION TOOL
// ============================================================================

import { scene, camera, renderer, raycaster, orbit, viewport } from '../engine.js';
import { selectedBlock, selectBlock, rebuildBlockMesh, syncInspectorFromSelected, blocks } from '../sceneManager.js';
import { snapSize, transformControl } from '../transformGizmo.js';
import { recordState } from '../historyManager.js';
import { showToast } from '../ipcBridge.js';

/**
 * @typedef {Object} FaceData
 * @property {THREE.Mesh} mesh - The target mesh owning the face.
 * @property {string} faceId - Face identifier ('+X', '-X', '+Y', '-Y', '+Z', '-Z').
 * @property {string} axis - Dimension axis modified ('X', 'Y', or 'Z').
 * @property {number} sign - Directional sign of the face normal (+1 or -1).
 * @property {THREE.Vector3} normalLocal - Local-space face normal vector.
 * @property {THREE.Vector3} normalWorld - Normalized world-space face normal vector.
 * @property {THREE.Vector3} centerLocal - Local-space center position of the face.
 * @property {THREE.Vector3} centerWorld - World-space center position of the face.
 * @property {number} width - Face width in local space.
 * @property {number} height - Face height in local space.
 * @property {string} faceDesc - Human-readable description (e.g. 'Front Face (+Y)').
 */

/** @type {FaceData|null} Currently hovered face under the cursor. */
let hoveredFaceData = null;

/** @type {FaceData|null} Currently selected face displaying the extrusion gizmo. */
let selectedFaceData = null;

/** @type {boolean} Flag indicating whether an active push/pull gizmo drag is in progress. */
let isPushPullDragging = false;

/** @type {THREE.Mesh|null} Translucent overlay quad showing cursor face hover preview. */
let hoverOverlayMesh = null;

/** @type {THREE.Group|null} High-contrast Cyan Blueprint overlay with border outline on selected face. */
let selectedOverlayGroup = null;

/** @type {THREE.Group|null} 3D normal gizmo arrow group positioned at the face center. */
let gizmoArrowGroup = null;

/** @type {THREE.Mesh|null} Interactive collision/hit mesh for grabbing the gizmo arrow. */
let gizmoHitMesh = null;

/** @type {boolean} Indicates whether the mouse is currently hovering over the gizmo arrow. */
let isGizmoHovered = false;

// Drag tracking state
/** @type {THREE.Vector3} Cached world position of the face center when dragging begins. */
let dragStartCenterWorld = new THREE.Vector3();

/** @type {THREE.Vector3} Cached mesh position when dragging begins. */
let dragStartMeshPos = new THREE.Vector3();

/** @type {{x: number, y: number, z: number}} Cached block dimensions prior to extrusion drag. */
let dragStartSizes = { x: 100, y: 100, z: 100 };

/** @type {THREE.Plane} Mathematical interaction plane used for projecting camera rays. */
let dragInteractionPlane = new THREE.Plane();

/** @type {THREE.Vector3} Initial 3D intersection point on interaction plane at pointerdown. */
let dragStartHitPoint = new THREE.Vector3();

// ============================================================================
// GIZMO & OVERLAY INITIALIZATION
// ============================================================================

/**
 * Creates and initializes the hover overlay quad mesh.
 * @returns {THREE.Mesh} The hover overlay mesh.
 */
function getOrCreateHoverOverlay() {
  if (!hoverOverlayMesh) {
    const geo = new THREE.PlaneGeometry(1, 1);
    const mat = new THREE.MeshBasicMaterial({
      color: 0x00e5ff,
      transparent: true,
      opacity: 0.18,
      depthTest: true,
      depthWrite: false,
      side: THREE.DoubleSide
    });
    hoverOverlayMesh = new THREE.Mesh(geo, mat);
    hoverOverlayMesh.renderOrder = 998;
    hoverOverlayMesh.visible = false;
    scene.add(hoverOverlayMesh);
  }
  return hoverOverlayMesh;
}

/**
 * Creates and initializes the selected face Cyan Blueprint overlay group.
 * @returns {THREE.Group} The selected overlay group.
 */
function getOrCreateSelectedOverlay() {
  if (!selectedOverlayGroup) {
    selectedOverlayGroup = new THREE.Group();

    // Translucent fill
    const fillGeo = new THREE.PlaneGeometry(1, 1);
    const fillMat = new THREE.MeshBasicMaterial({
      color: 0x00e5ff,
      transparent: true,
      opacity: 0.35,
      depthTest: true,
      depthWrite: false,
      side: THREE.DoubleSide
    });
    const fillMesh = new THREE.Mesh(fillGeo, fillMat);
    fillMesh.name = 'selectedOverlayFill';
    fillMesh.renderOrder = 998;

    // Crisp high-contrast border
    const edgeGeo = new THREE.EdgesGeometry(fillGeo);
    const edgeMat = new THREE.LineBasicMaterial({ color: 0x00e5ff, linewidth: 2 });
    const edgeLine = new THREE.LineSegments(edgeGeo, edgeMat);
    edgeLine.name = 'selectedOverlayEdge';
    edgeLine.renderOrder = 999;

    selectedOverlayGroup.add(fillMesh);
    selectedOverlayGroup.add(edgeLine);
    selectedOverlayGroup.visible = false;
    scene.add(selectedOverlayGroup);
  }
  return selectedOverlayGroup;
}

/**
 * Creates and initializes the 3D normal gizmo arrow group at the center of the selected face.
 * @returns {THREE.Group} The gizmo arrow group.
 */
function getOrCreateGizmoArrow() {
  if (!gizmoArrowGroup) {
    gizmoArrowGroup = new THREE.Group();

    // 1. Shaft (Cylinder, native orientation is along Y)
    const shaftGeo = new THREE.CylinderGeometry(2.5, 2.5, 60, 16);
    shaftGeo.translate(0, 30, 0); // anchor base at y=0
    const shaftMat = new THREE.MeshBasicMaterial({
      color: 0x00e5ff,
      depthTest: false,
      depthWrite: false
    });
    const shaftMesh = new THREE.Mesh(shaftGeo, shaftMat);
    shaftMesh.name = 'gizmoShaft';
    shaftMesh.renderOrder = 1000;
    gizmoArrowGroup.add(shaftMesh);

    // 2. Cone Tip (Cone, native orientation is along Y)
    const coneGeo = new THREE.ConeGeometry(6.5, 24, 16);
    coneGeo.translate(0, 60 + 12, 0);
    const coneMat = new THREE.MeshBasicMaterial({
      color: 0x00e5ff,
      depthTest: false,
      depthWrite: false
    });
    const coneMesh = new THREE.Mesh(coneGeo, coneMat);
    coneMesh.name = 'gizmoCone';
    coneMesh.renderOrder = 1000;
    gizmoArrowGroup.add(coneMesh);

    // 3. Tip Grab Sphere
    const sphereGeo = new THREE.SphereGeometry(7.5, 16, 16);
    sphereGeo.translate(0, 60 + 12, 0);
    const sphereMat = new THREE.MeshBasicMaterial({
      color: 0xffffff,
      depthTest: false,
      depthWrite: false
    });
    const sphereMesh = new THREE.Mesh(sphereGeo, sphereMat);
    sphereMesh.name = 'gizmoSphere';
    sphereMesh.renderOrder = 1001;
    gizmoArrowGroup.add(sphereMesh);

    // 4. Invisible Forgiving Raycast Hit Handle
    const hitGeo = new THREE.CylinderGeometry(14, 14, 95, 12);
    hitGeo.translate(0, 47.5, 0);
    const hitMat = new THREE.MeshBasicMaterial({ visible: false });
    gizmoHitMesh = new THREE.Mesh(hitGeo, hitMat);
    gizmoHitMesh.name = 'gizmoHitTarget';
    gizmoArrowGroup.add(gizmoHitMesh);

    gizmoArrowGroup.visible = false;
    scene.add(gizmoArrowGroup);
  }
  return gizmoArrowGroup;
}

/**
 * Updates the highlight color of the gizmo arrow parts based on hover state.
 * @param {boolean} hovered - Whether the gizmo handle is hovered.
 */
function setGizmoVisualHover(hovered) {
  isGizmoHovered = hovered;
  if (!gizmoArrowGroup) return;

  const color = hovered ? 0xffea00 : 0x00e5ff;
  const shaft = gizmoArrowGroup.getObjectByName('gizmoShaft');
  const cone = gizmoArrowGroup.getObjectByName('gizmoCone');
  const sphere = gizmoArrowGroup.getObjectByName('gizmoSphere');

  if (shaft) shaft.material.color.setHex(color);
  if (cone) cone.material.color.setHex(color);
  if (sphere) sphere.material.color.setHex(hovered ? 0xffea00 : 0xffffff);
}

// ============================================================================
// FACE DETECTION & GEOMETRIC CALCULATIONS
// ============================================================================

/**
 * Analyzes a raycast intersection and identifies the targeted face and its dimensions.
 * @param {THREE.Intersection} hit - Intersection result from Three.js Raycaster.
 * @returns {FaceData|null} Parsed face metadata, or null if unsupported.
 */
export function identifyFaceFromIntersection(hit) {
  if (!hit || !hit.object || !hit.face) return null;
  const mesh = hit.object;
  const shapeType = mesh.userData.shapeType || 'box';

  // Support Box primitives, planar caps of Cylinders, and planar faces of Ramps
  if (shapeType !== 'box' && shapeType !== 'cylinder' && shapeType !== 'ramp') {
    return null;
  }

  // Transform face normal into local mesh coordinates
  const localHitNormal = hit.face.normal.clone();
  const absX = Math.abs(localHitNormal.x);
  const absY = Math.abs(localHitNormal.y);
  const absZ = Math.abs(localHitNormal.z);

  let localNormal = new THREE.Vector3();
  let faceId = '';
  let axis = 'X';
  let sign = 1;

  if (absX >= absY && absX >= absZ) {
    sign = localHitNormal.x > 0 ? 1 : -1;
    localNormal.set(sign, 0, 0);
    faceId = sign > 0 ? '+X' : '-X';
    axis = 'X';
  } else if (absY >= absX && absY >= absZ) {
    sign = localHitNormal.y > 0 ? 1 : -1;
    localNormal.set(0, sign, 0);
    faceId = sign > 0 ? '+Y' : '-Y';
    axis = 'Z'; // Height in Blockout Studio engine
  } else {
    sign = localHitNormal.z > 0 ? 1 : -1;
    localNormal.set(0, 0, sign);
    faceId = sign > 0 ? '+Z' : '-Z';
    axis = 'Y'; // Length/depth in Blockout Studio engine
  }

  const sizeX = mesh.userData.sizeX || 100;
  const sizeY = mesh.userData.sizeY || 100;
  const sizeZ = mesh.userData.sizeZ || 100;

  let centerLocal = new THREE.Vector3();
  let width = 100;
  let height = 100;
  let faceDesc = '';

  if (shapeType === 'box') {
    if (faceId === '+X') {
      centerLocal.set(sizeX / 2, sizeZ / 2, 0);
      width = sizeY; height = sizeZ;
      faceDesc = 'Right Face (+X)';
    } else if (faceId === '-X') {
      centerLocal.set(-sizeX / 2, sizeZ / 2, 0);
      width = sizeY; height = sizeZ;
      faceDesc = 'Left Face (-X)';
    } else if (faceId === '+Y') {
      centerLocal.set(0, sizeZ, 0);
      width = sizeX; height = sizeY;
      faceDesc = 'Top Face (+Z Height)';
    } else if (faceId === '-Y') {
      centerLocal.set(0, 0, 0);
      width = sizeX; height = sizeY;
      faceDesc = 'Bottom Face (-Z Height)';
    } else if (faceId === '+Z') {
      centerLocal.set(0, sizeZ / 2, sizeY / 2);
      width = sizeX; height = sizeZ;
      faceDesc = 'Front Face (+Y Length)';
    } else if (faceId === '-Z') {
      centerLocal.set(0, sizeZ / 2, -sizeY / 2);
      width = sizeX; height = sizeZ;
      faceDesc = 'Back Face (-Y Length)';
    }
  } else if (shapeType === 'cylinder') {
    // Only top and bottom circular caps are planar
    if (faceId === '+Y') {
      centerLocal.set(0, sizeZ, 0);
      width = sizeX; height = sizeX;
      faceDesc = 'Cylinder Top Cap';
    } else if (faceId === '-Y') {
      centerLocal.set(0, 0, 0);
      width = sizeX; height = sizeX;
      faceDesc = 'Cylinder Bottom Cap';
    } else {
      return null; // Curved surface
    }
  } else if (shapeType === 'ramp') {
    const hw = sizeX / 2;
    const hl = sizeY / 2;
    if (faceId === '-Y') { // bottom
      centerLocal.set(0, 0, 0);
      width = sizeX; height = sizeY;
      faceDesc = 'Ramp Bottom Face';
    } else if (faceId === '-Z') { // back vertical
      centerLocal.set(0, sizeZ / 2, -hl);
      width = sizeX; height = sizeZ;
      faceDesc = 'Ramp Back Face';
    } else if (faceId === '+X') { // right side
      centerLocal.set(hw, sizeZ / 3, 0);
      width = sizeY; height = sizeZ;
      faceDesc = 'Ramp Right Side';
    } else if (faceId === '-X') { // left side
      centerLocal.set(-hw, sizeZ / 3, 0);
      width = sizeY; height = sizeZ;
      faceDesc = 'Ramp Left Side';
    } else {
      return null; // Sloped surface
    }
  }

  mesh.updateMatrixWorld(true);
  const centerWorld = centerLocal.clone().applyMatrix4(mesh.matrixWorld);
  const normalWorld = localNormal.clone().transformDirection(mesh.matrixWorld).normalize();

  return {
    mesh,
    faceId,
    axis,
    sign,
    normalLocal: localNormal,
    normalWorld,
    centerLocal,
    centerWorld,
    width,
    height,
    faceDesc
  };
}

/**
 * Updates the position, orientation, and dimensions of a quad overlay mesh.
 * @param {THREE.Object3D} overlayObj - Target overlay group or mesh.
 * @param {FaceData} face - Face data providing position, normal, and dimensions.
 */
function updateOverlayTransform(overlayObj, face) {
  if (!overlayObj || !face) return;

  // Rebuild plane geometry to match face dimensions if dimensions changed
  const fillMesh = (overlayObj.getObjectByName && overlayObj.getObjectByName('selectedOverlayFill')) || overlayObj;
  const edgeLine = overlayObj.getObjectByName ? overlayObj.getObjectByName('selectedOverlayEdge') : null;

  if (fillMesh && fillMesh.geometry) {
    const p = fillMesh.geometry.parameters;
    if (!p || p.width !== face.width || p.height !== face.height) {
      fillMesh.geometry.dispose();
      fillMesh.geometry = new THREE.PlaneGeometry(face.width, face.height);
    }
  }
  if (edgeLine) {
    if (!edgeLine.userData.lastWidth || edgeLine.userData.lastWidth !== face.width || edgeLine.userData.lastHeight !== face.height) {
      edgeLine.geometry.dispose();
      const planeGeo = new THREE.PlaneGeometry(face.width, face.height);
      edgeLine.geometry = new THREE.EdgesGeometry(planeGeo);
      planeGeo.dispose();
      edgeLine.userData.lastWidth = face.width;
      edgeLine.userData.lastHeight = face.height;
    }
  }

  // Align plane orientation with mesh orientation and face normal
  const defaultNormal = new THREE.Vector3(0, 0, 1);
  const qLocal = new THREE.Quaternion().setFromUnitVectors(defaultNormal, face.normalLocal);
  overlayObj.quaternion.copy(face.mesh.quaternion).multiply(qLocal);

  // Offset slightly along face normal to eliminate z-fighting
  overlayObj.position.copy(face.centerWorld).addScaledVector(face.normalWorld, 0.25);
  overlayObj.visible = true;
}

/**
 * Positions and aligns the 3D normal gizmo arrow at the center of the active face.
 * @param {FaceData} face - Active selected face.
 */
function updateGizmoTransform(face) {
  const gizmo = getOrCreateGizmoArrow();
  if (!face) {
    gizmo.visible = false;
    return;
  }

  // Align arrow along the face normal
  const defaultArrowDir = new THREE.Vector3(0, 1, 0);
  gizmo.quaternion.setFromUnitVectors(defaultArrowDir, face.normalWorld);
  gizmo.position.copy(face.centerWorld);
  gizmo.visible = true;
}

// ============================================================================
// POINTER EVENT HANDLERS
// ============================================================================

/**
 * Handles pointerdown events on the viewport for selecting a face or initiating gizmo extrusion.
 * @param {PointerEvent|MouseEvent} e - Pointer down event.
 */
export function handlePushPullPointerDown(e) {
  if (e.button !== 0) return; // Only left-click interacts

  // Ensure standard transform gizmo is detached so it never conflicts with the Push/Pull gizmo
  if (transformControl.object) {
    transformControl.detach();
  }

  // 1. If gizmo arrow is active, check if user clicked the gizmo arrow handle
  if (selectedFaceData && gizmoArrowGroup && gizmoArrowGroup.visible) {
    const gizmoHits = raycaster.intersectObjects(gizmoArrowGroup.children, true);
    if (gizmoHits.length > 0) {
      startGizmoDrag(e);
      return;
    }
  }

  // 2. Raycast against scene blocks to select a face
  const hits = raycaster.intersectObjects(blocks, false);
  if (hits.length > 0) {
    const face = identifyFaceFromIntersection(hits[0]);
    if (face) {
      selectBlock(face.mesh, false);
      selectedFaceData = face;

      const selOverlay = getOrCreateSelectedOverlay();
      updateOverlayTransform(selOverlay, face);
      updateGizmoTransform(face);

      // Hide hover overlay while face is selected
      if (hoverOverlayMesh) hoverOverlayMesh.visible = false;
    } else {
      showToast('Selected surface is non-planar for push/pull.', true);
    }
  } else {
    // Clicked in empty space: clear selected face and gizmo
    clearSelectedFace();
  }
}

/**
 * Initiates the 1-sided extrusion drag operation from the normal gizmo arrow.
 * @param {PointerEvent|MouseEvent} e - Pointer down event on the gizmo.
 */
function startGizmoDrag(e) {
  if (!selectedFaceData) return;

  isPushPullDragging = true;
  orbit.enabled = false;
  setGizmoVisualHover(true);

  const face = selectedFaceData;
  dragStartCenterWorld.copy(face.centerWorld);
  dragStartMeshPos.copy(face.mesh.position);
  dragStartSizes = {
    x: face.mesh.userData.sizeX,
    y: face.mesh.userData.sizeY,
    z: face.mesh.userData.sizeZ
  };

  // Build an interaction plane passing through the face center facing the camera
  const camDir = new THREE.Vector3().subVectors(camera.position, face.centerWorld).normalize();
  dragInteractionPlane.setFromNormalAndCoplanarPoint(camDir, face.centerWorld);

  // Compute initial ray intersection point on the interaction plane
  const startPt = new THREE.Vector3();
  if (raycaster.ray.intersectPlane(dragInteractionPlane, startPt)) {
    dragStartHitPoint.copy(startPt);
  } else {
    dragStartHitPoint.copy(face.centerWorld);
  }

  const liveMeasure = document.getElementById('live-measure');
  if (liveMeasure) {
    liveMeasure.textContent = `${face.faceDesc}: +0 cm`;
    liveMeasure.style.display = 'block';
  }
}

/**
 * Handles pointermove events for hover highlighting and active gizmo extrusion dragging.
 * @param {PointerEvent|MouseEvent} e - Pointer move event.
 */
export function handlePushPullPointerMove(e) {
  // 1. If actively dragging the gizmo arrow, perform single-sided extrusion
  if (isPushPullDragging && selectedFaceData) {
    processGizmoDrag(e);
    return;
  }

  // 2. Check if hovering over the gizmo arrow handle
  if (selectedFaceData && gizmoArrowGroup && gizmoArrowGroup.visible) {
    const gizmoHits = raycaster.intersectObjects(gizmoArrowGroup.children, true);
    if (gizmoHits.length > 0) {
      setGizmoVisualHover(true);
      document.body.style.cursor = 'grab';
      if (hoverOverlayMesh) hoverOverlayMesh.visible = false;
      return;
    } else if (isGizmoHovered) {
      setGizmoVisualHover(false);
      document.body.style.cursor = '';
    }
  }

  // 3. Update cursor hover highlight over scene blocks
  const hits = raycaster.intersectObjects(blocks, false);
  if (hits.length > 0) {
    const face = identifyFaceFromIntersection(hits[0]);
    if (face) {
      hoveredFaceData = face;
      // Do not duplicate highlight if this face is already selected
      if (!selectedFaceData || selectedFaceData.mesh !== face.mesh || selectedFaceData.faceId !== face.faceId) {
        const hoverOverlay = getOrCreateHoverOverlay();
        updateOverlayTransform(hoverOverlay, face);
        document.body.style.cursor = 'pointer';
      } else {
        if (hoverOverlayMesh) hoverOverlayMesh.visible = false;
      }
    } else {
      clearHover();
    }
  } else {
    clearHover();
  }
}

/**
 * Calculates single-sided extrusion displacement and updates geometry while anchoring the opposite face.
 * @param {PointerEvent|MouseEvent} e - Pointer move event during drag.
 */
function processGizmoDrag(e) {
  const face = selectedFaceData;
  if (!face) return;

  const currentHitPt = new THREE.Vector3();
  if (!raycaster.ray.intersectPlane(dragInteractionPlane, currentHitPt)) return;

  // Project 3D mouse displacement onto the face normal vector
  const moveVec = new THREE.Vector3().subVectors(currentHitPt, dragStartHitPoint);
  let rawDelta = moveVec.dot(face.normalWorld);

  // Apply grid snapping (unless Shift is held for smooth extrusion)
  let delta = rawDelta;
  if (!e.shiftKey && snapSize > 0) {
    delta = Math.round(rawDelta / snapSize) * snapSize;
  }

  const mesh = face.mesh;
  let newSize = 100;
  let deltaClamped = delta;

  // Determine which axis dimension changes and clamp to 10cm minimum thickness
  if (face.axis === 'X') {
    newSize = Math.max(10, dragStartSizes.x + delta);
    deltaClamped = newSize - dragStartSizes.x;
    mesh.userData.sizeX = newSize;
  } else if (face.axis === 'Y') {
    newSize = Math.max(10, dragStartSizes.y + delta);
    deltaClamped = newSize - dragStartSizes.y;
    mesh.userData.sizeY = newSize;
  } else if (face.axis === 'Z') {
    newSize = Math.max(10, dragStartSizes.z + delta);
    deltaClamped = newSize - dragStartSizes.z;
    mesh.userData.sizeZ = newSize;
  }

  // 1-Sided Anchor Math: Shift mesh center so opposite face stays fixed in 3D world space
  let deltaPosLocal = new THREE.Vector3();

  if (face.faceId === '+X') {
    deltaPosLocal.set(deltaClamped / 2, 0, 0);
  } else if (face.faceId === '-X') {
    deltaPosLocal.set(-deltaClamped / 2, 0, 0);
  } else if (face.faceId === '+Z') { // Front in Three.js (sizeY)
    deltaPosLocal.set(0, 0, deltaClamped / 2);
  } else if (face.faceId === '-Z') { // Back in Three.js (sizeY)
    deltaPosLocal.set(0, 0, -deltaClamped / 2);
  } else if (face.faceId === '+Y') { // Top in Three.js (sizeZ): Base is anchored at y=0, no position shift needed!
    deltaPosLocal.set(0, 0, 0);
  } else if (face.faceId === '-Y') { // Bottom in Three.js (sizeZ): Top stays anchored, shift origin down
    deltaPosLocal.set(0, -deltaClamped, 0);
  }

  // Apply rotation to local displacement and set mesh position
  const deltaPosWorld = deltaPosLocal.clone().applyQuaternion(mesh.quaternion);
  mesh.position.copy(dragStartMeshPos).add(deltaPosWorld);
  mesh.updateMatrixWorld(true);

  // Rebuild mesh BufferGeometry and sync details panel
  rebuildBlockMesh(mesh);
  syncInspectorFromSelected();

  // Face center in world space moves strictly along the face normal by deltaClamped (zero lag)
  face.centerWorld.copy(dragStartCenterWorld).addScaledVector(face.normalWorld, deltaClamped);

  // Update selection overlay and gizmo positions in real time
  if (selectedOverlayGroup) updateOverlayTransform(selectedOverlayGroup, face);
  if (gizmoArrowGroup) updateGizmoTransform(face);

  // Update floating measurement badge
  const liveMeasure = document.getElementById('live-measure');
  if (liveMeasure) {
    const signStr = deltaClamped >= 0 ? '+' : '';
    liveMeasure.textContent = `${face.faceDesc}: ${signStr}${Math.round(deltaClamped)} cm (${newSize} cm)`;
  }
}

/**
 * Finalizes the push/pull drag, commits an undo snapshot, and keeps the gizmo active on the new face center.
 */
export function handlePushPullPointerUp() {
  if (isPushPullDragging) {
    isPushPullDragging = false;
    orbit.enabled = true;
    setGizmoVisualHover(false);

    const liveMeasure = document.getElementById('live-measure');
    if (liveMeasure) liveMeasure.style.display = 'none';

    if (selectedFaceData) {
      const mesh = selectedFaceData.mesh;
      const u = mesh.userData;
      const changed = (u.sizeX !== dragStartSizes.x || u.sizeY !== dragStartSizes.y || u.sizeZ !== dragStartSizes.z);

      if (changed) {
        recordState(`Push/Pull ${u.name} ${selectedFaceData.faceDesc}`);
      }

      // Re-anchor selected face with fresh coordinates on newly sized mesh
      mesh.updateMatrixWorld(true);
      const recheckedFace = identifyFaceFromIntersection({
        object: mesh,
        face: { normal: selectedFaceData.normalLocal }
      });
      if (recheckedFace) {
        selectedFaceData = recheckedFace;
        if (selectedOverlayGroup) updateOverlayTransform(selectedOverlayGroup, selectedFaceData);
        if (gizmoArrowGroup) updateGizmoTransform(selectedFaceData);
      }
    }
  }
}

// ============================================================================
// CLEANUP & STATE MANAGEMENT
// ============================================================================

/**
 * Clears cursor hover state and hides the hover overlay quad.
 */
function clearHover() {
  hoveredFaceData = null;
  if (hoverOverlayMesh) hoverOverlayMesh.visible = false;
  if (!isPushPullDragging && !isGizmoHovered) {
    document.body.style.cursor = '';
  }
}

/**
 * Clears the active selected face and hides the selection overlay and gizmo arrow.
 */
export function clearSelectedFace() {
  selectedFaceData = null;
  isPushPullDragging = false;
  if (selectedOverlayGroup) selectedOverlayGroup.visible = false;
  if (gizmoArrowGroup) gizmoArrowGroup.visible = false;
  clearHover();
}

/**
 * Completely unmounts all Push/Pull overlays, arrows, and event states when switching tools.
 */
export function cleanupPushPullTool() {
  clearSelectedFace();
  if (hoverOverlayMesh) {
    scene.remove(hoverOverlayMesh);
    if (hoverOverlayMesh.geometry) hoverOverlayMesh.geometry.dispose();
    if (hoverOverlayMesh.material) hoverOverlayMesh.material.dispose();
    hoverOverlayMesh = null;
  }
  if (selectedOverlayGroup) {
    scene.remove(selectedOverlayGroup);
    selectedOverlayGroup.traverse((child) => {
      if (child.geometry) child.geometry.dispose();
      if (child.material) child.material.dispose();
    });
    selectedOverlayGroup = null;
  }
  if (gizmoArrowGroup) {
    scene.remove(gizmoArrowGroup);
    gizmoArrowGroup.traverse((child) => {
      if (child.geometry) child.geometry.dispose();
      if (child.material) child.material.dispose();
    });
    gizmoArrowGroup = null;
    gizmoHitMesh = null;
  }
  orbit.enabled = true;
  document.body.style.cursor = '';
  const liveMeasure = document.getElementById('live-measure');
  if (liveMeasure) liveMeasure.style.display = 'none';
}
