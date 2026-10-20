/**
 * @file lights.js
 * @brief PointLight and SpotLight actor creation, attenuation geometry visualization, and shadow allocation.
 */

// ============================================================================
// STATIC & STATIONARY LIGHTS (PointLight & SpotLight)
// ============================================================================

import { scene } from './engine.js';
import { blocks, selectBlock, refreshOutliner } from './sceneManager.js';
import { recordState } from './historyManager.js';

/**
 * @type {number}
 * @brief Auto-incrementing numerical identifier for generated light actor names.
 */
let lightCounter = 1;

/**
 * @brief Instantiates and places a new light actor into the scene with attenuation wireframe indicators.
 * @param {string} [type='point'] Light type ('point', 'point_light', 'spot', or 'spot_light').
 * @param {string} [mobility='Static'] Light mobility classification ('Static', 'Stationary', or 'Dynamic').
 * @param {boolean} [shouldRecord=true] Whether to push an entry to the history manager undo stack.
 * @returns {THREE.Group} The newly created light actor group.
 */
export function spawnLight(type = 'point', mobility = 'Static', shouldRecord = true) {
  const isSpot = type === 'spot' || type === 'spot_light';
  const group = new THREE.Group();

  const color = 0xffe6a3;
  const intensity = isSpot ? 1.5 : 1.0;
  const radius = 600; // 600 cm (6 meters)

  // 1. Core bulb mesh
  const bulbMat = new THREE.MeshBasicMaterial({ color: 0xfff0b3 });
  const bulbMesh = new THREE.Mesh(new THREE.SphereGeometry(14, 16, 16), bulbMat);
  group.add(bulbMesh);

  // 2. Attenuation wireframe radius indicator
  const radiusMat = new THREE.LineBasicMaterial({ color: 0xffaa00, transparent: true, opacity: 0.25 });
  const radiusWire = new THREE.LineSegments(new THREE.WireframeGeometry(new THREE.SphereGeometry(radius, 16, 12)), radiusMat);
  group.add(radiusWire);

  // 3. Selection outline helper
  const edgeHelper = new THREE.LineSegments(
    new THREE.EdgesGeometry(new THREE.SphereGeometry(16, 8, 8)),
    new THREE.LineBasicMaterial({ color: 0x181a20 })
  );
  group.add(edgeHelper);

  // 4. Actual Three.js Light
  let light;
  if (isSpot) {
    light = new THREE.SpotLight(color, intensity, radius, Math.PI / 4, 0.4);
    light.position.set(0, 0, 0);
    group.add(light);
  } else {
    light = new THREE.PointLight(color, intensity, radius);
    light.position.set(0, 0, 0);
    group.add(light);
  }

  // Position light above the floor
  group.position.set(0, 250, 0);

  group.userData = {
    name: `${isSpot ? 'SpotLight' : 'PointLight'}_${mobility}_${lightCounter++}`,
    shapeType: isSpot ? 'spot_light' : 'point_light',
    mobility: mobility, // 'Static' or 'Stationary'
    intensity: 5000,
    radius: radius,
    color: '#ffe6a3',
    sizeX: 30, sizeY: 30, sizeZ: 30 // bounds placeholder for outliner
  };

  scene.add(group);
  blocks.push(group);
  selectBlock(group);
  refreshOutliner();
  if (shouldRecord) {
    recordState(`Add ${mobility} ${isSpot ? 'SpotLight' : 'PointLight'}`);
  }
  return group;
}

/**
 * @brief Restores a light actor from a serialized snapshot state object.
 * @param {Object} data Serialized light metadata including position, rotation, color, and intensity.
 * @returns {THREE.Group} The restored light actor group.
 */
export function restoreLightFromData(data) {
  const isSpot = data.shapeType === 'spot' || data.shapeType === 'spot_light';
  const group = new THREE.Group();

  const color = 0xffe6a3;
  const intensity = isSpot ? 1.5 : 1.0;
  const radius = data.radius || 600;

  const bulbMat = new THREE.MeshBasicMaterial({ color: 0xfff0b3 });
  const bulbMesh = new THREE.Mesh(new THREE.SphereGeometry(14, 16, 16), bulbMat);
  group.add(bulbMesh);

  const radiusMat = new THREE.LineBasicMaterial({ color: 0xffaa00, transparent: true, opacity: 0.25 });
  const radiusWire = new THREE.LineSegments(new THREE.WireframeGeometry(new THREE.SphereGeometry(radius, 16, 12)), radiusMat);
  group.add(radiusWire);

  const edgeHelper = new THREE.LineSegments(
    new THREE.EdgesGeometry(new THREE.SphereGeometry(16, 8, 8)),
    new THREE.LineBasicMaterial({ color: 0x181a20 })
  );
  group.add(edgeHelper);

  let light;
  if (isSpot) {
    light = new THREE.SpotLight(color, intensity, radius, Math.PI / 4, 0.4);
    light.position.set(0, 0, 0);
    group.add(light);
  } else {
    light = new THREE.PointLight(color, intensity, radius);
    light.position.set(0, 0, 0);
    group.add(light);
  }

  group.position.set(data.posX, data.posY, data.posZ);
  group.rotation.set(data.rotX || 0, data.rotY || 0, data.rotZ || 0);

  group.userData = {
    name: data.name,
    shapeType: data.shapeType,
    mobility: data.mobility || 'Static',
    intensity: data.intensity || 5000,
    radius: radius,
    color: data.color || '#ffe6a3',
    sizeX: data.sizeX || 30,
    sizeY: data.sizeY || 30,
    sizeZ: data.sizeZ || 30
  };

  const m = data.name.match(/_(\d+)$/);
  if (m) {
    const num = parseInt(m[1], 10);
    if (!isNaN(num) && num >= lightCounter) {
      lightCounter = num + 1;
    }
  }

  scene.add(group);
  blocks.push(group);
  return group;
}

/**
 * @brief Configures real-time shadow casting flags based on engine scalability limits.
 * @param {number} [maxShadowLights=4] Maximum number of light actors allowed to cast shadows simultaneously.
 */
export function updateLightShadowLimits(maxShadowLights = 4) {
  let shadowCount = 0;
  for (const block of blocks) {
    if (block.userData && (block.userData.shapeType === 'point_light' || block.userData.shapeType === 'spot_light')) {
      for (const child of block.children) {
        if (child.isLight && (child.isPointLight || child.isSpotLight)) {
          const allowShadow = shadowCount < maxShadowLights;
          child.castShadow = allowShadow;
          if (allowShadow) {
            child.shadow.mapSize.width = 1024;
            child.shadow.mapSize.height = 1024;
            shadowCount++;
          }
        }
      }
    }
  }
}
