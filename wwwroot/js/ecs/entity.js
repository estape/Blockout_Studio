// ============================================================================
// ECS ENTITY WRAPPER & COMPONENT MANAGEMENT
// ============================================================================

import {
  TransformComponent, MeshComponent, ColliderComponent,
  createComponentByType
} from './components.js';

export function ensureEntityComponents(object) {
  if (!object) return;
  if (!object.userData) object.userData = {};

  if (!Array.isArray(object.userData.components)) {
    object.userData.components = [];

    // 1. Base Transform Component
    const transform = new TransformComponent({
      posX: object.position ? object.position.x : 0,
      posY: object.position ? object.position.z : 0, // UE5 Y = Three.js Z
      posZ: object.position ? object.position.y : 0, // UE5 Z = Three.js Y (Z-Up)
      rotX: object.rotation ? object.rotation.x : 0,
      rotY: object.rotation ? object.rotation.z : 0,
      rotZ: object.rotation ? object.rotation.y : 0
    });
    object.userData.components.push(transform);

    // 2. Base Mesh Component if applicable
    if (object.userData.shapeType && !object.userData.shapeType.includes('light')) {
      const meshComp = new MeshComponent({
        shapeType: object.userData.shapeType,
        sizeX: object.userData.sizeX || 100,
        sizeY: object.userData.sizeY || 100,
        sizeZ: object.userData.sizeZ || 100
      });
      object.userData.components.push(meshComp);

      // Default Box/Shape Collider Component for automatic physical presence
      const colliderComp = new ColliderComponent({
        colliderType: (object.userData.shapeType === 'sphere' ? 'sphere' : 'box'),
        sizeX: object.userData.sizeX || 100,
        sizeY: object.userData.sizeY || 100,
        sizeZ: object.userData.sizeZ || 100,
        radius: (object.userData.sizeX || 100) / 2
      });
      object.userData.components.push(colliderComp);
    }
  }

  return object.userData.components;
}

export function getComponent(object, type) {
  if (!object || !object.userData || !Array.isArray(object.userData.components)) return null;
  return object.userData.components.find((c) => c.type === type) || null;
}

export function hasComponent(object, type) {
  return getComponent(object, type) !== null;
}

export function addComponent(object, component) {
  if (!object) return null;
  ensureEntityComponents(object);

  // Prevent multiple components of the same type unless supported
  const existingIdx = object.userData.components.findIndex((c) => c.type === component.type);
  if (existingIdx >= 0) {
    object.userData.components[existingIdx] = component;
  } else {
    object.userData.components.push(component);
  }
  return component;
}

export function removeComponent(object, type) {
  if (!object || !object.userData || !Array.isArray(object.userData.components)) return false;
  if (type === 'Transform') return false; // Transform is mandatory
  const idx = object.userData.components.findIndex((c) => c.type === type);
  if (idx >= 0) {
    object.userData.components.splice(idx, 1);
    return true;
  }
  return false;
}

export function serializeEntityComponents(object) {
  if (!object || !object.userData || !Array.isArray(object.userData.components)) return [];
  return object.userData.components.map((c) => {
    return typeof c.serialize === 'function' ? c.serialize() : { ...c };
  });
}

export function deserializeEntityComponents(object, rawComponents) {
  if (!object || !Array.isArray(rawComponents)) return;
  if (!object.userData) object.userData = {};
  object.userData.components = [];

  for (const raw of rawComponents) {
    const comp = createComponentByType(raw.type, raw);
    if (comp) {
      object.userData.components.push(comp);
    }
  }

  // Ensure mandatory transform exists
  if (!hasComponent(object, 'Transform')) {
    ensureEntityComponents(object);
  }
}
