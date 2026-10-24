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
    /** @type {number} Ground penetration skin offset in centimeters. */
    this.skinWidth = 2.0;
    /** @type {number} Cosine of maximum walkable slope angle (45 degrees threshold). */
    this.slopeLimit = 0.707;
    /** @type {Set<string>} Active set of pressed keyboard key codes. */
    this.keys = new Set();
    /** @type {number} Accumulated horizontal mouse movement delta for yaw rotation. */
    this.mouseDeltaX = 0;
    /** @type {number} Accumulated vertical mouse movement delta for pitch rotation. */
    this.mouseDeltaY = 0;
    /** @type {boolean} Indicates whether the browser pointer lock is currently acquired. */
    this.isPointerLocked = false;
    /** @type {{x: number, y: number, z: number}} Default fallback spawn position. */
    this.spawnPosition = { x: 0, y: 22, z: 0 };

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
   * @brief Computes the world-space bounding box for an obstacle collider.
   * @param {THREE.Object3D} entity - Scene entity owning the collider.
   * @param {Object} collider - Attached ColliderComponent.
   * @returns {{minX: number, maxX: number, minY: number, maxY: number, minZ: number, maxZ: number, centerX: number, centerY: number, centerZ: number, halfX: number, halfY: number, halfZ: number}}
   */
  getColliderAABB(entity, collider) {
    const halfX = (collider.sizeX || 100) / 2;
    const halfY = (collider.sizeZ || 100) / 2; // Three.js Y is height (sizeZ in engine)
    const halfZ = (collider.sizeY || 100) / 2; // Three.js Z is depth (sizeY in engine)

    const offX = collider.centerOffsetX || 0;
    const offY = collider.centerOffsetZ !== undefined ? collider.centerOffsetZ : halfY;
    const offZ = collider.centerOffsetY || 0;

    const centerX = entity.position.x + offX;
    const centerY = entity.position.y + offY;
    const centerZ = entity.position.z + offZ;

    return {
      minX: centerX - halfX,
      maxX: centerX + halfX,
      minY: centerY - halfY,
      maxY: centerY + halfY,
      minZ: centerZ - halfZ,
      maxZ: centerZ + halfZ,
      centerX,
      centerY,
      centerZ,
      halfX,
      halfY,
      halfZ
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

    // 4. Horizontal Move with 45cm Step-Climbing and Wall Normal Sliding
    let moveX = wishDirX * dt;
    let moveZ = wishDirZ * dt;

    const feetY = entity.position.y;
    const charRadius = cc.radius || 30;
    const charHeight = cc.height || 180;
    const stepHeight = cc.stepHeight || 45;

    // Sweep against scene colliders
    for (const item of colliders) {
      if (item.entity === entity || item.collider.isTrigger) continue;
      const aabb = this.getColliderAABB(item.entity, item.collider);

      // Support surface check: If the obstacle is at or below feet level, it is a floor!
      // Upward floor normals must NEVER block or zero horizontal velocity!
      if (aabb.maxY <= feetY + 0.5) {
        continue;
      }

      // If obstacle bottom is above the player's head, skip:
      if (aabb.minY >= feetY + charHeight) {
        continue;
      }

      // Step-climbing check: if top is within stepHeight, player can step onto it
      const stepDiff = aabb.maxY - feetY;
      if (stepDiff > 0 && stepDiff <= stepHeight) {
        // Step obstacle: allowed to climb onto, does not block lateral movement
        continue;
      }

      // True Lateral Wall: aabb.maxY > feetY + stepHeight and aabb.minY < feetY + charHeight
      const testX = entity.position.x + moveX;
      const testZ = entity.position.z + moveZ;

      const closestX = Math.max(aabb.minX, Math.min(testX, aabb.maxX));
      const closestZ = Math.max(aabb.minZ, Math.min(testZ, aabb.maxZ));

      const dx = testX - closestX;
      const dz = testZ - closestZ;
      const distSq = dx * dx + dz * dz;
      const reqDist = charRadius + this.skinWidth;

      if (distSq < reqDist * reqDist) {
        let normX = 0;
        let normZ = 0;
        let depth = 0;

        if (distSq > 0.0001) {
          const dist = Math.sqrt(distSq);
          normX = dx / dist;
          normZ = dz / dist;
          depth = reqDist - dist;
        } else {
          // Inside wall box: push out along minimum penetration axis
          const penLeft = testX - (aabb.minX - reqDist);
          const penRight = (aabb.maxX + reqDist) - testX;
          const penBack = testZ - (aabb.minZ - reqDist);
          const penFront = (aabb.maxZ + reqDist) - testZ;

          const minPen = Math.min(penLeft, penRight, penBack, penFront);
          depth = minPen;
          if (minPen === penLeft) normX = -1;
          else if (minPen === penRight) normX = 1;
          else if (minPen === penBack) normZ = -1;
          else normZ = 1;
        }

        // Contact normal is horizontal (normY = 0 < slopeLimit 0.707)
        // Project movement along wall tangent (slide along wall instead of getting stuck)
        const dot = moveX * normX + moveZ * normZ;
        if (dot < 0) {
          moveX -= dot * normX;
          moveZ -= dot * normZ;
        }

        // Resolve penetration pushout
        moveX += normX * depth;
        moveZ += normZ * depth;
      }
    }

    entity.position.x += moveX;
    entity.position.z += moveZ;

    cc.velocityX = dt > 0 ? (moveX / dt) : 0;
    cc.velocityZ = dt > 0 ? (moveZ / dt) : 0;

    // 5. Vertical Move & Ground Check with Skin Width
    const prevFeetY = entity.position.y;
    entity.position.y += cc.velocityY * dt;

    let highestGroundY = -Infinity;
    let foundGround = false;

    for (const item of colliders) {
      if (item.entity === entity || item.collider.isTrigger) continue;
      const aabb = this.getColliderAABB(item.entity, item.collider);

      // Check if player footprint overlaps obstacle in horizontal plane
      const closeX = Math.max(aabb.minX, Math.min(entity.position.x, aabb.maxX));
      const closeZ = Math.max(aabb.minZ, Math.min(entity.position.z, aabb.maxZ));
      const distSq = (entity.position.x - closeX) ** 2 + (entity.position.z - closeZ) ** 2;

      if (distSq <= (charRadius * charRadius)) {
        let surfaceY = aabb.maxY;

        // Ramp slope surface interpolation
        if (item.entity.userData && item.entity.userData.shapeType === 'ramp') {
          const localZ = entity.position.z - aabb.centerZ;
          const rampFraction = Math.max(0, Math.min(1, (localZ + aabb.halfZ) / (2 * aabb.halfZ)));
          surfaceY = aabb.minY + rampFraction * (2 * aabb.halfY);
        }

        // Ceiling collision check when moving upward
        if (cc.velocityY > 0) {
          const headY = entity.position.y + charHeight;
          const prevHeadY = prevFeetY + charHeight;
          if (prevHeadY <= aabb.minY + 5 && headY >= aabb.minY) {
            entity.position.y = aabb.minY - charHeight - this.skinWidth;
            cc.velocityY = 0;
          }
        } else {
          // Downward motion / resting on ground
          const landingTolerance = Math.max(10, Math.abs(cc.velocityY * dt) + 15);
          if (prevFeetY >= surfaceY - 15 && entity.position.y <= surfaceY + (cc.isGrounded ? stepHeight : landingTolerance)) {
            if (surfaceY > highestGroundY) {
              highestGroundY = surfaceY;
              foundGround = true;
            }
          }
        }
      }
    }

    if (foundGround) {
      const targetGroundedY = highestGroundY + this.skinWidth;
      entity.position.y = targetGroundedY;
      cc.velocityY = 0;
      cc.isGrounded = true;
    } else {
      cc.isGrounded = false;
    }

    // KillZ boundary check (Z < -2000 cm in engine coordinates, pos.y < -2000 in Three.js)
    if (entity.position.y < -2000) {
      const spawnPos = entity.userData.spawnPosition || this.spawnPosition || { x: 0, y: 22, z: 0 };
      entity.position.set(spawnPos.x, spawnPos.y, spawnPos.z);
      cc.velocityX = 0;
      cc.velocityY = 0;
      cc.velocityZ = 0;
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
