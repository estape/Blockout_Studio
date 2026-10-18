// ============================================================================
// ECS PHYSICS SYSTEM (UE5 Centimeter Units: g = -980 cm/s², AABB / Sphere)
// ============================================================================

import { getComponent } from '../entity.js';

export class PhysicsSystem {
  constructor() {
    this.gravity = -980.0; // cm/s^2 (UE5 standard gravity)
    this.terminalVelocity = -2500.0; // cm/s
    this.collisionEvents = [];
  }

  update(entities, dt) {
    if (dt <= 0 || dt > 0.1) dt = 0.016; // Clamp large delta stalls
    this.collisionEvents.length = 0;

    // 1. Gather all colliders
    const colliders = [];
    for (const ent of entities) {
      const col = getComponent(ent, 'Collider');
      const rb = getComponent(ent, 'Rigidbody');
      if (col) {
        colliders.push({ entity: ent, collider: col, rb: rb });
      }
    }

    // 2. Simulate Dynamic Rigidbodies
    for (const item of colliders) {
      const { entity, rb, collider } = item;
      if (!rb || rb.mobility !== 'Dynamic') continue;

      // Apply Gravity
      if (rb.useGravity) {
        rb.velocityY += this.gravity * dt;
        if (rb.velocityY < this.terminalVelocity) {
          rb.velocityY = this.terminalVelocity;
        }
      }

      // Apply Drag
      const dragFactor = Math.max(0, 1.0 - rb.drag * dt);
      rb.velocityX *= dragFactor;
      rb.velocityZ *= dragFactor;

      // Integrate Position
      entity.position.x += rb.velocityX * dt;
      entity.position.y += rb.velocityY * dt;
      entity.position.z += rb.velocityZ * dt;

      // Ground plane check (y = 0 in Three.js represents floor level)
      const halfH = (collider.sizeZ || 100) / 2;
      if (entity.position.y < halfH) {
        entity.position.y = halfH;
        if (rb.velocityY < 0) rb.velocityY = 0;
      }

      // Resolve vs all other colliders
      for (const other of colliders) {
        if (other.entity === entity) continue;
        const hit = this.checkCollision(item, other);
        if (hit.collided) {
          this.collisionEvents.push({ a: entity, b: other.entity });
          if (!collider.isTrigger && !other.collider.isTrigger) {
            // Push dynamic object out along collision normal
            entity.position.x += hit.normal.x * hit.depth;
            entity.position.y += hit.normal.y * hit.depth;
            entity.position.z += hit.normal.z * hit.depth;

            if (hit.normal.y > 0.7 && rb.velocityY < 0) {
              rb.velocityY = 0; // Landed on top
            }
          }
        }
      }
    }

    return this.collisionEvents;
  }

  checkCollision(itemA, itemB) {
    const colA = itemA.collider;
    const colB = itemB.collider;
    const posA = itemA.entity.position;
    const posB = itemB.entity.position;

    // Sphere-Sphere
    if (colA.colliderType === 'sphere' && colB.colliderType === 'sphere') {
      const dx = posA.x - posB.x;
      const dy = posA.y - posB.y;
      const dz = posA.z - posB.z;
      const distSq = dx * dx + dy * dy + dz * dz;
      const rSum = colA.radius + colB.radius;
      if (distSq < rSum * rSum) {
        const dist = Math.sqrt(distSq) || 0.001;
        return {
          collided: true,
          normal: { x: dx / dist, y: dy / dist, z: dz / dist },
          depth: rSum - dist
        };
      }
      return { collided: false };
    }

    // Default: AABB vs AABB (UE5 cm units)
    // Three.js: X = width, Y = height, Z = length
    const hAx = (colA.sizeX || 100) / 2;
    const hAy = (colA.sizeZ || 100) / 2; // Three.js Y is height (sizeZ)
    const hAz = (colA.sizeY || 100) / 2;

    const hBx = (colB.sizeX || 100) / 2;
    const hBy = (colB.sizeZ || 100) / 2;
    const hBz = (colB.sizeY || 100) / 2;

    const dx = posA.x - posB.x;
    const px = (hAx + hBx) - Math.abs(dx);
    if (px <= 0) return { collided: false };

    const dy = posA.y - posB.y;
    const py = (hAy + hBy) - Math.abs(dy);
    if (py <= 0) return { collided: false };

    const dz = posA.z - posB.z;
    const pz = (hAz + hBz) - Math.abs(dz);
    if (pz <= 0) return { collided: false };

    // Minimum penetration axis
    if (px < py && px < pz) {
      const sign = dx > 0 ? 1 : -1;
      return { collided: true, normal: { x: sign, y: 0, z: 0 }, depth: px };
    } else if (py < pz) {
      const sign = dy > 0 ? 1 : -1;
      return { collided: true, normal: { x: 0, y: sign, z: 0 }, depth: py };
    } else {
      const sign = dz > 0 ? 1 : -1;
      return { collided: true, normal: { x: 0, y: 0, z: sign }, depth: pz };
    }
  }
}

export const physicsSystem = new PhysicsSystem();
