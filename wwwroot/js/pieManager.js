/**
 * @file pieManager.js
 * @brief Play-In-Editor (PIE) lifecycle manager coordinating runtime simulation, possession, and scene restoration.
 */

// ============================================================================
// PLAY-IN-EDITOR (PIE) LIFECYCLE MANAGER
// ============================================================================

import { scene, camera, renderer, orbit, subGrid, majorGrid } from './engine.js';
import { blocks, selectBlock, createBlock } from './sceneManager.js';
import { transformControl } from './transformGizmo.js';
import { serializeScene, deserializeScene } from './historyManager.js';
import { showToast } from './ipcBridge.js';
import { ensureEntityComponents, getComponent, addComponent } from './ecs/entity.js';
import { CharacterControllerComponent, ColliderComponent } from './ecs/components.js';
import { characterControllerSystem } from './ecs/systems/characterControllerSystem.js';
import { physicsSystem } from './ecs/systems/physicsSystem.js';
import { scriptSystem } from './ecs/systems/scriptSystem.js';
import { audioSystem } from './ecs/systems/audioSystem.js';

/**
 * @enum {string}
 * @brief Simulation states for Play-In-Editor mode.
 */
export const PieState = {
  STOPPED: 'STOPPED',
  PLAYING: 'PLAYING',
  PAUSED: 'PAUSED'
};

/**
 * @type {string}
 * @brief Current simulation state (STOPPED, PLAYING, or PAUSED).
 */
export let currentPieState = PieState.STOPPED;

/**
 * @type {Object|null}
 * @brief Pre-simulation scene snapshot used to restore pristine editor state upon exiting PIE.
 */
let prePlaySnapshot = null;

/**
 * @type {THREE.Object3D|null}
 * @brief Active possessed character entity driven by player input.
 */
let possessedEntity = null;

/**
 * @type {THREE.Vector3}
 * @brief Cached camera position prior to entering PIE mode.
 */
let savedCameraPos = new THREE.Vector3();

/**
 * @type {THREE.Euler}
 * @brief Cached camera rotation prior to entering PIE mode.
 */
let savedCameraRot = new THREE.Euler();

/**
 * @type {THREE.Vector3}
 * @brief Cached OrbitControls target prior to entering PIE mode.
 */
let savedOrbitTarget = new THREE.Vector3();

/**
 * @brief Checks if Play-In-Editor simulation is active (either playing or paused).
 * @returns {boolean} True if PIE is currently active; otherwise false.
 */
export function isPieActive() {
  return currentPieState !== PieState.STOPPED;
}

/**
 * @brief Checks if Play-In-Editor simulation is actively running and unpaused.
 * @returns {boolean} True if PIE is currently running; otherwise false.
 */
export function isPiePlaying() {
  return currentPieState === PieState.PLAYING;
}

/**
 * @brief Starts Play-In-Editor simulation, takes a non-destructive scene snapshot, and possesses a player avatar.
 */
export function startPie() {
  if (isPieActive()) return;

  // 1. Snapshot Editor Scene for 100% Non-Destructive Restore
  prePlaySnapshot = serializeScene();
  savedCameraPos.copy(camera.position);
  savedCameraRot.copy(camera.rotation);
  savedOrbitTarget.copy(orbit.target);

  // 2. Hide Editor Gizmos & Helpers
  transformControl.detach();
  orbit.enabled = false;
  if (subGrid) subGrid.visible = false;
  if (majorGrid) majorGrid.visible = false;
  selectBlock(null);

  // 3. Find or Spawn Possessable Character Controller
  possessedEntity = null;
  for (const b of blocks) {
    if (getComponent(b, 'CharacterController')) {
      possessedEntity = b;
      break;
    }
  }

  if (!possessedEntity) {
    // Spawn default 180cm Dummy player actor
    const playerBlock = createBlock('BP_Player_Dummy', 'cylinder', 60, 60, 180, 0, 0, 0, false);
    ensureEntityComponents(playerBlock);
    addComponent(playerBlock, new CharacterControllerComponent({
      height: 180,
      radius: 30,
      stepHeight: 45,
      walkSpeed: 450,
      sprintSpeed: 750,
      jumpVelocity: 600
    }));
    addComponent(playerBlock, new ColliderComponent({
      colliderType: 'capsule',
      height: 180,
      radius: 30
    }));
    possessedEntity = playerBlock;
  }

  // 4. Request Pointer Lock on Viewport Canvas
  const canvas = renderer.domElement;
  if (canvas.requestPointerLock) {
    canvas.requestPointerLock();
  }

  // 5. Initialize ECS Systems
  characterControllerSystem.attachInputListeners();
  characterControllerSystem.isPointerLocked = true;
  scriptSystem.start(blocks);
  audioSystem.ensureContext();
  audioSystem.playSound('chime');

  // 6. Update State & UI Controls
  currentPieState = PieState.PLAYING;
  updatePieUi();

  const pieHud = document.getElementById('pie-hud');
  if (pieHud) pieHud.style.display = 'block';

  showToast('Play In Editor Started (Esc to Stop, WASD to Move)');
}

/**
 * @brief Toggles simulation between paused and playing states.
 */
export function pausePie() {
  if (!isPieActive()) return;

  if (currentPieState === PieState.PLAYING) {
    currentPieState = PieState.PAUSED;
    if (document.exitPointerLock) document.exitPointerLock();
    characterControllerSystem.isPointerLocked = false;
    showToast('Simulation Paused');
  } else if (currentPieState === PieState.PAUSED) {
    currentPieState = PieState.PLAYING;
    const canvas = renderer.domElement;
    if (canvas.requestPointerLock) canvas.requestPointerLock();
    characterControllerSystem.isPointerLocked = true;
    showToast('Simulation Resumed');
  }
  updatePieUi();
}

/**
 * @brief Stops Play-In-Editor simulation and restores the scene to its pre-play snapshot state.
 */
export function stopPie() {
  if (!isPieActive()) return;

  // 1. Release Pointer Lock
  if (document.exitPointerLock) {
    document.exitPointerLock();
  }
  characterControllerSystem.isPointerLocked = false;
  characterControllerSystem.detachInputListeners();

  // 2. Stop Scripts & Audio
  scriptSystem.stop();

  // 3. Restore Pre-Play Scene Snapshot
  if (prePlaySnapshot) {
    deserializeScene(prePlaySnapshot);
    prePlaySnapshot = null;
  }
  possessedEntity = null;

  // 4. Restore Editor Camera & Orbit Controls
  orbit.enabled = true;
  camera.position.copy(savedCameraPos);
  camera.rotation.copy(savedCameraRot);
  orbit.target.copy(savedOrbitTarget);
  orbit.update();

  // 5. Re-enable Editor Helpers
  if (subGrid) subGrid.visible = true;
  if (majorGrid) majorGrid.visible = true;

  // 6. Update State & UI
  currentPieState = PieState.STOPPED;
  updatePieUi();

  const pieHud = document.getElementById('pie-hud');
  if (pieHud) pieHud.style.display = 'none';

  showToast('Play In Editor Stopped (Scene Restored)');
}

/**
 * @brief Ticks character controller, physics, scripts, and audio listener each simulation frame.
 * @param {number} dt Delta time in seconds since the previous frame.
 */
export function updatePie(dt) {
  if (currentPieState !== PieState.PLAYING) return;

  // 1. Update Character Controller
  if (possessedEntity) {
    const colliders = [];
    for (const b of blocks) {
      const col = getComponent(b, 'Collider');
      if (col) colliders.push({ entity: b, collider: col });
    }
    characterControllerSystem.update(possessedEntity, camera, colliders, dt);
  }

  // 2. Update Rigidbodies & Collisions
  const collisions = physicsSystem.update(blocks, dt);

  // 3. Trigger Script Collision Events
  for (const { a, b } of collisions) {
    scriptSystem.handleCollision(a, b);
  }

  // 4. Update Scripts
  scriptSystem.update(dt, characterControllerSystem.keys);

  // 5. Update 3D Audio Listener
  audioSystem.updateListener(camera);

  // 6. Update PIE HUD live speed / ground status
  updatePieHud();
}

/**
 * @brief Synchronizes toolbar Play/Pause/Stop button visual states with the current PIE state.
 */
function updatePieUi() {
  const btnPlay = document.getElementById('btn-pie-play');
  const btnPause = document.getElementById('btn-pie-pause');
  const btnStop = document.getElementById('btn-pie-stop');

  if (btnPlay) {
    btnPlay.classList.toggle('active', currentPieState === PieState.PLAYING);
    btnPlay.disabled = (currentPieState === PieState.PLAYING);
  }
  if (btnPause) {
    btnPause.disabled = (currentPieState === PieState.STOPPED);
    btnPause.classList.toggle('active', currentPieState === PieState.PAUSED);
  }
  if (btnStop) {
    btnStop.disabled = (currentPieState === PieState.STOPPED);
  }
}

/**
 * @brief Updates the head-up display showing live dummy movement speed and grounding status.
 */
function updatePieHud() {
  const speedEl = document.getElementById('pie-speed');
  const groundEl = document.getElementById('pie-ground');
  if (!possessedEntity) return;

  const cc = getComponent(possessedEntity, 'CharacterController');
  if (cc && speedEl && groundEl) {
    const isSprint = characterControllerSystem.keys.has('ShiftLeft') || characterControllerSystem.keys.has('ShiftRight');
    const isMoving = characterControllerSystem.keys.has('KeyW') || characterControllerSystem.keys.has('KeyS') ||
                     characterControllerSystem.keys.has('KeyA') || characterControllerSystem.keys.has('KeyD');
    const spd = isMoving ? (isSprint ? cc.sprintSpeed : cc.walkSpeed) : 0;
    speedEl.textContent = `${spd} cm/s`;
    groundEl.textContent = cc.isGrounded ? 'Grounded' : 'Airborne';
    groundEl.style.color = cc.isGrounded ? '#2ecc71' : '#f58220';
  }
}

// Pointer lock change handler
document.addEventListener('pointerlockchange', () => {
  const canvas = renderer.domElement;
  characterControllerSystem.isPointerLocked = (document.pointerLockElement === canvas);
});

// Global Keyboard Shortcut: Alt+P to toggle PIE, Esc to Stop
window.addEventListener('keydown', (e) => {
  if (e.altKey && (e.code === 'KeyP' || e.key === 'p')) {
    e.preventDefault();
    if (isPieActive()) stopPie();
    else startPie();
  } else if (e.code === 'Escape' && isPieActive()) {
    e.preventDefault();
    stopPie();
  }
});
