/**
 * @file characterControllerSystem.js
 * @brief ECS Character Controller System for player avatars (180cm Dummy, step-climbing, mouse-look).
 */

// ============================================================================
// ECS CHARACTER CONTROLLER SYSTEM (180cm Dummy: 180cm H, 60cm W, 45cm Step)
// ============================================================================

import { getComponent } from '../entity.js';

/**
 * @class CharacterControllerSystem
 * @brief Handles first-person dummy physics, AABB sweeps, step-climbing, and pointer-lock mouse look.
 */
export class CharacterControllerSystem {
  /**
   * @brief Initializes a new instance of the CharacterControllerSystem class.
   */
  constructor() {
    /** @type {number} Downward gravitational acceleration in cm/s^2. */
    this.gravity = -980.0;
    /** @type {Set<string>} Active set of pressed keyboard key codes. */
    this.keys = new Set();
    /** @type {number} Accumulated horizontal mouse movement delta for yaw rotation. */
    this.mouseDeltaX = 0;
    /** @type {number} Accumulated vertical mouse movement delta for pitch rotation. */
    this.mouseDeltaY = 0;
    /** @type {boolean} Indicates whether the browser pointer lock is currently acquired. */
    this.isPointerLocked = false;

    /** @type {(e: KeyboardEvent) => void} Event listener callback for keydown events. */
    this.onKeyDown = (e) => { this.keys.add(e.code); };
    /** @type {(e: KeyboardEvent) => void} Event listener callback for keyup events. */
    this.onKeyUp = (e) => { this.keys.delete(e.code); };
    /** @type {(e: MouseEvent) => void} Event listener callback for mousemove events. */
    this.onMouseMove = (e) => {
      if (this.isPointerLocked) {
        this.mouseDeltaX += e.movementX || 0;
        this.mouseDeltaY += e.movementY || 0;
      }
    };
  }

  /**
   * @brief Binds keyboard and mouse event listeners to the global window object.
   */
  attachInputListeners() {
    window.addEventListener('keydown', this.onKeyDown);
    window.addEventListener('keyup', this.onKeyUp);
    window.addEventListener('mousemove', this.onMouseMove);
  }

  /**
   * @brief Removes keyboard and mouse event listeners from the global window object and clears inputs.
   */
  detachInputListeners() {
    window.removeEventListener('keydown', this.onKeyDown);
    window.removeEventListener('keyup', this.onKeyUp);
    window.removeEventListener('mousemove', this.onMouseMove);
    this.keys.clear();
    this.mouseDeltaX = 0;
    this.mouseDeltaY = 0;
  }

  /**
   * @brief Updates the character controller entity physics, step-climbing, and camera attachment.
   * @param {THREE.Object3D} entity The character controller root entity.
   * @param {THREE.Camera} camera The viewport camera to synchronize with dummy eye-level.
   * @param {Array<{entity: THREE.Object3D, collider: Object}>} colliders Array of world obstacle colliders.
   * @param {number} dt Delta time in seconds since the last simulation frame.
   */
  update(entity, camera, colliders, dt) {
    if (!entity) return;
    const cc = getComponent(entity, 'CharacterController');
    if (!cc) return;

    if (dt <= 0 || dt > 0.1) dt = 0.016;

    // 1. Process Mouse Look
    const sens = cc.mouseSensitivity || 0.0022;
    cc.yaw -= this.mouseDeltaX * sens;
    cc.pitch -= this.mouseDeltaY * sens;
    const maxPitch = 85 * (Math.PI / 180);
    cc.pitch = Math.max(-maxPitch, Math.min(maxPitch, cc.pitch));
    this.mouseDeltaX = 0;
    this.mouseDeltaY = 0;

    // 2. Compute Desired Horizontal Movement Direction
    let moveForward = 0;
    let moveRight = 0;
    if (this.keys.has('KeyW') || this.keys.has('ArrowUp')) moveForward += 1;
    if (this.keys.has('KeyS') || this.keys.has('ArrowDown')) moveForward -= 1;
    if (this.keys.has('KeyD') || this.keys.has('ArrowRight')) moveRight += 1;
    if (this.keys.has('KeyA') || this.keys.has('ArrowLeft')) moveRight -= 1;

    const isSprinting = this.keys.has('ShiftLeft') || this.keys.has('ShiftRight');
    const speed = isSprinting ? cc.sprintSpeed : cc.walkSpeed;

    // Camera-relative forward & right vectors on XZ ground plane
    const sinY = Math.sin(cc.yaw);
    const cosY = Math.cos(cc.yaw);
    const forwardX = -sinY;
    const forwardZ = -cosY;
    const rightX = cosY;
    const rightZ = -sinY;

    let wishDirX = forwardX * moveForward + rightX * moveRight;
    let wishDirZ = forwardZ * moveForward + rightZ * moveRight;
    const wishLen = Math.sqrt(wishDirX * wishDirX + wishDirZ * wishDirZ);
    if (wishLen > 0.001) {
      wishDirX = (wishDirX / wishLen) * speed;
      wishDirZ = (wishDirZ / wishLen) * speed;
    } else {
      wishDirX = 0;
      wishDirZ = 0;
    }

    // 3. Jump and Gravity
    if (cc.isGrounded && this.keys.has('Space')) {
      cc.velocityY = cc.jumpVelocity;
      cc.isGrounded = false;
    } else {
      cc.velocityY += this.gravity * dt;
      if (cc.velocityY < -2500) cc.velocityY = -2500;
    }

    // 4. Horizontal Move with 45cm Step-Climbing
    const targetPosX = entity.position.x + wishDirX * dt;
    const targetPosZ = entity.position.z + wishDirZ * dt;

    const feetY = entity.position.y;
    const charRadius = cc.radius || 30;
    const charHeight = cc.height || 180;
    const stepHeight = cc.stepHeight || 45;

    // Sweep against scene colliders
    let canMoveX = true;
    let canMoveZ = true;

    for (const item of colliders) {
      if (item.entity === entity || item.collider.isTrigger) continue;
      const bCol = item.collider;
      const bPos = item.entity.position;

      const halfBx = (bCol.sizeX || 100) / 2;
      const halfBy = (bCol.sizeZ || 100) / 2; // Three.js Y is sizeZ
      const halfBz = (bCol.sizeY || 100) / 2;

      const topY = bPos.y + halfBy;
      const botY = bPos.y - halfBy;

      // Check vertical overlap
      if (feetY + charHeight > botY && feetY < topY) {
        // Step check: if top is within 45 cm of feet, step up!
        const stepDiff = topY - feetY;
        if (stepDiff > 0 && stepDiff <= stepHeight) {
          // If we are grounded and moving towards obstacle, step onto surface
          if (cc.isGrounded && (Math.abs(targetPosX - bPos.x) < halfBx + charRadius) && (Math.abs(targetPosZ - bPos.z) < halfBz + charRadius)) {
            entity.position.y = topY;
            cc.isGrounded = true;
            cc.velocityY = 0;
            continue;
          }
        }

        // Check X penetration
        if (Math.abs(targetPosX - bPos.x) < halfBx + charRadius && Math.abs(entity.position.z - bPos.z) < halfBz + charRadius) {
          canMoveX = false;
        }
        // Check Z penetration
        if (Math.abs(entity.position.x - bPos.x) < halfBx + charRadius && Math.abs(targetPosZ - bPos.z) < halfBz + charRadius) {
          canMoveZ = false;
        }
      }
    }

    if (canMoveX) entity.position.x = targetPosX;
    if (canMoveZ) entity.position.z = targetPosZ;

    // 5. Vertical Move & Ground Check
    entity.position.y += cc.velocityY * dt;

    // Ground plane check (y = 0)
    let groundY = 0;
    for (const item of colliders) {
      if (item.entity === entity || item.collider.isTrigger) continue;
      const bCol = item.collider;
      const bPos = item.entity.position;
      const halfBx = (bCol.sizeX || 100) / 2;
      const halfBy = (bCol.sizeZ || 100) / 2;
      const halfBz = (bCol.sizeY || 100) / 2;

      const topY = bPos.y + halfBy;
      if (entity.position.y <= topY + 5 && entity.position.y >= topY - 15) {
        if (Math.abs(entity.position.x - bPos.x) <= halfBx && Math.abs(entity.position.z - bPos.z) <= halfBz) {
          if (topY > groundY) groundY = topY;
        }
      }
    }

    if (entity.position.y <= groundY) {
      entity.position.y = groundY;
      cc.velocityY = 0;
      cc.isGrounded = true;
    } else {
      cc.isGrounded = false;
    }

    // 6. Synchronize Camera (Eye level: feet + 160 cm)
    if (camera) {
      camera.position.x = entity.position.x;
      camera.position.y = entity.position.y + 160;
      camera.position.z = entity.position.z;

      // Camera rotation: First-person pitch + yaw
      const euler = new THREE.Euler(cc.pitch, cc.yaw, 0, 'YXZ');
      camera.quaternion.setFromEuler(euler);
    }

    // Synchronize entity rotation
    entity.rotation.y = cc.yaw;
  }
}

/**
 * @type {CharacterControllerSystem}
 * @brief Singleton instance of the CharacterControllerSystem.
 */
export const characterControllerSystem = new CharacterControllerSystem();
