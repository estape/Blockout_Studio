/**
 * @file entity.js
 * @brief Entity Component System (ECS) entity container and component lifecycle management.
 */

import {
  TransformComponent, MeshComponent, ColliderComponent,
  createComponentByType
} from './components.js';

/**
 * Initializes default Transform and Mesh/Collider components on a 3D scene object if missing.
 * @param {THREE.Object3D} object - Scene node representing the entity.
 * @returns {Array<Object>|undefined} List of attached components.
 */
export function ensureEntityComponents(object) {
  if (!object) return;
  if (!object.userData) object.userData = {};

  if (!Array.isArray(object.userData.components)) {
    object.userData.components = [];

    // 1. Base Transform Component
    const transform = new TransformComponent({
      posX: object.position ? object.position.x : 0,
      posY: object.position ? object.position.z : 0, // Engine Y = Three.js Z
      posZ: object.position ? object.position.y : 0, // Engine Z = Three.js Y (Z-Up)
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
        centerOffsetX: 0,
        centerOffsetY: 0,
        centerOffsetZ: (object.userData.sizeZ || 100) / 2,
        radius: (object.userData.sizeX || 100) / 2
      });
      object.userData.components.push(colliderComp);
      if (object.geometry) {
        syncActorColliderFromGeometry(object);
      }
    }
  }

  return object.userData.components;
}

/**
 * Retrieves a component by type name from an entity.
 * @param {THREE.Object3D} object - Target scene object.
 * @param {string} type - Component type name (e.g. 'Transform', 'RigidBody').
 * @returns {Object|null} Matching component instance or null.
 */
export function getComponent(object, type) {
  if (!object || !object.userData || !Array.isArray(object.userData.components)) return null;
  return object.userData.components.find((c) => c.type === type) || null;
}

/**
 * Checks if an entity possesses a component of a given type.
 * @param {THREE.Object3D} object - Target scene object.
 * @param {string} type - Component type name.
 * @returns {boolean} True if the component exists on the entity.
 */
export function hasComponent(object, type) {
  return getComponent(object, type) !== null;
}

/**
 * Adds or replaces a component on an entity.
 * @param {THREE.Object3D} object - Target scene object.
 * @param {Object} component - Component instance to add.
 * @returns {Object|null} The attached component.
 */
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

/**
 * Removes a component of the specified type from an entity.
 * @param {THREE.Object3D} object - Target scene object.
 * @param {string} type - Component type to remove (Transform cannot be removed).
 * @returns {boolean} True if removed, false otherwise.
 */
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

/**
 * Serializes all attached entity components into plain JSON-serializable objects.
 * @param {THREE.Object3D} object - Target scene object.
 * @returns {Array<Object>} List of serialized component data objects.
 */
export function serializeEntityComponents(object) {
  if (!object || !object.userData || !Array.isArray(object.userData.components)) return [];
  return object.userData.components.map((c) => {
    return typeof c.serialize === 'function' ? c.serialize() : { ...c };
  });
}

/**
 * Deserializes an array of raw component configurations and attaches them to an entity.
 * @param {THREE.Object3D} object - Target scene object.
 * @param {Array<Object>} rawComponents - Raw component property objects.
 */
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

/**
 * Recomputes the local bounding box from the vertex buffer and updates attached Collider,
 * Mesh, and Transform components to match modified geometry.
 * @param {THREE.Object3D} object - Target scene object with geometry.
 */
export function syncActorColliderFromGeometry(object) {
  if (!object || !object.geometry) return;
  object.geometry.computeBoundingBox();
  const bbox = object.geometry.boundingBox;
  if (!bbox) return;

  const extentX = Math.max(1, bbox.max.x - bbox.min.x);
  const extentY = Math.max(1, bbox.max.y - bbox.min.y); // Height in Three.js (sizeZ in engine)
  const extentZ = Math.max(1, bbox.max.z - bbox.min.z); // Depth in Three.js (sizeY in engine)

  const centerX = (bbox.min.x + bbox.max.x) / 2;
  const centerY = (bbox.min.y + bbox.max.y) / 2; // Height offset
  const centerZ = (bbox.min.z + bbox.max.z) / 2; // Depth offset

  const col = getComponent(object, 'Collider');
  if (col) {
    col.sizeX = Math.round(extentX);
    col.sizeY = Math.round(extentZ);
    col.sizeZ = Math.round(extentY);
    col.centerOffsetX = Math.round(centerX * 100) / 100;
    col.centerOffsetY = Math.round(centerZ * 100) / 100;
    col.centerOffsetZ = Math.round(centerY * 100) / 100;
    if (col.colliderType === 'sphere') {
      col.radius = Math.round(extentX / 2);
    }
  }

  const meshComp = getComponent(object, 'Mesh');
  if (meshComp) {
    meshComp.sizeX = Math.round(extentX);
    meshComp.sizeY = Math.round(extentZ);
    meshComp.sizeZ = Math.round(extentY);
  }

  const transformComp = getComponent(object, 'Transform');
  if (transformComp) {
    transformComp.posX = Math.round(object.position.x);
    transformComp.posY = Math.round(object.position.z);
    transformComp.posZ = Math.round(object.position.y);
  }
}

