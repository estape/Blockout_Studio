// ============================================================================
// ECS CHARACTER CONTROLLER SYSTEM (UE5 Mannequin: 180cm H, 60cm W, 45cm Step)
// ============================================================================

import { getComponent } from '../entity.js';

export class CharacterControllerSystem {
  constructor() {
    this.gravity = -980.0; // cm/s^2
    this.keys = new Set();
    this.mouseDeltaX = 0;
    this.mouseDeltaY = 0;
    this.isPointerLocked = false;

    this.onKeyDown = (e) => { this.keys.add(e.code); };
    this.onKeyUp = (e) => { this.keys.delete(e.code); };
    this.onMouseMove = (e) => {
      if (this.isPointerLocked) {
        this.mouseDeltaX += e.movementX || 0;
        this.mouseDeltaY += e.movementY || 0;
      }
    };
  }

  attachInputListeners() {
    window.addEventListener('keydown', this.onKeyDown);
    window.addEventListener('keyup', this.onKeyUp);
    window.addEventListener('mousemove', this.onMouseMove);
  }

  detachInputListeners() {
    window.removeEventListener('keydown', this.onKeyDown);
    window.removeEventListener('keyup', this.onKeyUp);
    window.removeEventListener('mousemove', this.onMouseMove);
    this.keys.clear();
    this.mouseDeltaX = 0;
    this.mouseDeltaY = 0;
  }

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

export const characterControllerSystem = new CharacterControllerSystem();
