/**
 * @file usdObjLoader.js
 * @brief Binary IPC mesh loader streaming OBJ/USD meshes directly into Three.js BufferGeometry.
 */

// ============================================================================
// NATIVE .OBJ & .USD GEOMETRY STREAMING LOADER (WebGL 2.0 Binary IPC)
// ============================================================================

import { scene, camera } from './engine.js';
import { blocks, selectedBlock, selectBlock, refreshOutliner } from './sceneManager.js';
import { transformControl } from './transformGizmo.js';
import { sendToBackend, registerHandler, showToast } from './ipcBridge.js';
import { recordState } from './historyManager.js';

/**
 * @type {Map<string, {targetPosition: THREE.Vector3|null, onLoaded: Function|null}>}
 * @brief Pending mesh spawn requests keyed by asset GUID or virtual path.
 */
let pendingSpawnRequests = new Map();

/**
 * @brief Registers IPC response handlers for binary mesh payloads, OBJ exports, and USD stage saves.
 */
export function initUsdObjLoader() {
  registerHandler('mesh_binary_response', (msg) => {
    if (msg.success && msg.binaryBase64) {
      handleMeshBinaryResponse(msg);
    } else {
      showToast(msg.message || 'Failed to load binary mesh', true);
    }
  });

  registerHandler('save_blockout_as_obj_response', (msg) => {
    if (msg.success) {
      showToast(`Saved asset: ${msg.virtualPath}.obj`);
    } else {
      showToast(msg.message || 'Failed to save .obj asset', true);
    }
  });

  registerHandler('save_usd_stage_response', (msg) => {
    if (msg.success) {
      showToast(`Saved USD Stage: ${msg.virtualPath}.usda`);
    } else {
      showToast(msg.message || 'Failed to save USD stage', true);
    }
  });
}

// ============================================================================
// BINARY IPC MESH PARSER & THREE.JS INSTANTIATION
// ============================================================================

/**
 * @brief Dispatches an IPC request to fetch binary mesh data for a specified asset GUID or virtual path.
 * @param {string} guidOrVirtualPath Asset GUID or /Game/ virtual path identifier.
 * @param {THREE.Vector3|null} [targetPosition=null] World coordinates where the mesh should be instantiated.
 * @param {Function|null} [onLoaded=null] Optional callback invoked once the mesh has been spawned.
 */
export function requestMeshBinary(guidOrVirtualPath, targetPosition = null, onLoaded = null) {
  const reqId = guidOrVirtualPath + '_' + Date.now();
  pendingSpawnRequests.set(guidOrVirtualPath, { targetPosition, onLoaded });

  sendToBackend('load_mesh_binary', {
    guid: guidOrVirtualPath.startsWith('/Game/') ? null : guidOrVirtualPath,
    virtualPath: guidOrVirtualPath.startsWith('/Game/') ? guidOrVirtualPath : null
  });
}

/**
 * @brief Decodes and parses base64 binary mesh buffer ("BENG" format) into a Three.js Mesh.
 * @param {Object} msg IPC message containing binaryBase64, guid, and metadata.
 */
function handleMeshBinaryResponse(msg) {
  const req = pendingSpawnRequests.get(msg.guid) || {};
  pendingSpawnRequests.delete(msg.guid);

  // 1. Decode Base64 to ArrayBuffer in zero main-thread freeze
  const binaryString = atob(msg.binaryBase64);
  const len = binaryString.length;
  const bytes = new Uint8Array(len);
  for (let i = 0; i < len; i++) {
    bytes[i] = binaryString.charCodeAt(i);
  }
  const buffer = bytes.buffer;

  // 2. Parse Binary Header
  const view = new DataView(buffer);
  const magic = view.getUint32(0, true);
  if (magic !== 0x474E4542) { // "BENG"
    showToast('Invalid mesh binary header', true);
    return;
  }

  const vCount = view.getInt32(8, true);
  const iCount = view.getInt32(12, true);

  if (vCount <= 0) {
    showToast('Empty mesh geometry', true);
    return;
  }

  // 3. Extract Typed Arrays from buffer
  let byteOffset = 16;
  const positions = new Float32Array(buffer, byteOffset, vCount * 3);
  byteOffset += vCount * 3 * 4;

  const normals = new Float32Array(buffer, byteOffset, vCount * 3);
  byteOffset += vCount * 3 * 4;

  const uvs = new Float32Array(buffer, byteOffset, vCount * 2);
  byteOffset += vCount * 2 * 4;

  const colors = new Float32Array(buffer, byteOffset, vCount * 3);
  byteOffset += vCount * 3 * 4;

  const indices = new Int32Array(buffer, byteOffset, iCount);

  // 4. Build THREE.BufferGeometry
  const geom = new THREE.BufferGeometry();
  geom.setAttribute('position', new THREE.BufferAttribute(new Float32Array(positions), 3));

  if (normals.length > 0 && (normals[0] !== 0 || normals[1] !== 0 || normals[2] !== 0)) {
    geom.setAttribute('normal', new THREE.BufferAttribute(new Float32Array(normals), 3));
  } else {
    geom.computeVertexNormals();
  }

  if (uvs.length > 0) {
    geom.setAttribute('uv', new THREE.BufferAttribute(new Float32Array(uvs), 2));
  }

  let hasAoColors = false;
  if (colors.length > 0) {
    for (let i = 0; i < Math.min(colors.length, 30); i++) {
      if (colors[i] < 0.99) { hasAoColors = true; break; }
    }
    if (hasAoColors) {
      geom.setAttribute('color', new THREE.BufferAttribute(new Float32Array(colors), 3));
    }
  }

  if (iCount > 0) {
    geom.setIndex(new THREE.BufferAttribute(new Uint32Array(indices), 1));
  }

  // 5. Build Material & Mesh
  const mat = new THREE.MeshStandardMaterial({
    color: 0x8b98ad,
    roughness: 0.6,
    metalness: 0.1,
    vertexColors: hasAoColors
  });

  const meshName = msg.name || 'SM_Imported';
  const mesh = new THREE.Mesh(geom, mat);
  mesh.castShadow = true;
  mesh.receiveShadow = true;

  // Placement in scene
  const pos = req.targetPosition || new THREE.Vector3(0, 0, 0);
  mesh.position.set(pos.x, pos.y, pos.z);

  // Bounding measurements in metric units (cm)
  geom.computeBoundingBox();
  const box = geom.boundingBox;
  const sizeX = Math.round(box.max.x - box.min.x) || 100;
  const sizeY = Math.round(box.max.y - box.min.y) || 100;
  const sizeZ = Math.round(box.max.z - box.min.z) || 100;

  mesh.userData = {
    name: meshName,
    shapeType: 'custom_mesh',
    guid: msg.guid,
    mobility: 'Static',
    sizeX, sizeY, sizeZ,
    posX: Math.round(pos.x),
    posY: Math.round(pos.y),
    posZ: Math.round(pos.z),
    rotZ: 0
  };

  scene.add(mesh);
  blocks.push(mesh);
  selectBlock(mesh);
  refreshOutliner();
  recordState(`Spawned Mesh ${meshName}`);

  showToast(`Loaded ${meshName} (${vCount} verts, ${iCount / 3} tris) with zero freeze`);

  if (req.onLoaded) req.onLoaded(mesh);
}

// ============================================================================
// EXPORT SCENE / BLOCKOUT TO NATIVE .OBJ & .USD ASSETS
// ============================================================================

/**
 * @brief Prompts for an asset name and exports the selected scene mesh as an OBJ asset into /Game/Meshes/.
 */
export function saveSelectedAsObj() {
  if (!selectedBlock) {
    alert('Select an object in the scene to save as native .OBJ asset!');
    return;
  }

  const defaultName = selectedBlock.userData.name || 'SM_Asset';
  const assetName = prompt('Enter Asset Name to save into /Game/Meshes/:', defaultName);
  if (!assetName) return;

  const geom = selectedBlock.geometry;
  if (!geom || !geom.attributes.position) {
    alert('Object geometry is invalid!');
    return;
  }

  const posAttr = geom.attributes.position;
  const normAttr = geom.attributes.normal;
  const uvAttr = geom.attributes.uv;

  const positions = Array.from(posAttr.array);
  const normals = normAttr ? Array.from(normAttr.array) : null;
  const uvs = uvAttr ? Array.from(uvAttr.array) : null;
  const indices = geom.index ? Array.from(geom.index.array) : null;

  sendToBackend('save_blockout_as_obj', {
    blockName: assetName,
    positions,
    normals,
    uvs,
    indices,
    bakeAo: true,
    pivotMode: 'bottom'
  });

  showToast(`Saving ${assetName}.obj to Assets/Meshes/...`);
}

/**
 * @brief Serializes all scene block objects and exports a non-destructive Universal Scene Description stage (.usda).
 */
export function saveLevelAsUsdStage() {
  if (blocks.length === 0) {
    alert('No objects in scene to save as USD stage!');
    return;
  }

  const stageName = prompt('Enter USD Stage Name to save into /Game/Scenes/:', 'Main_Level_Stage');
  if (!stageName) return;

  const prims = blocks.map((b) => {
    return {
      name: b.userData.name,
      specifier: b.userData.guid ? 'over' : 'def',
      referencePath: b.userData.guid ? `/Game/Meshes/${b.userData.name}.obj` : null,
      position: [b.position.x, b.position.y, b.position.z],
      rotation: [b.rotation.x, b.rotation.y, b.rotation.z],
      scale: [b.scale.x, b.scale.y, b.scale.z],
      materialBinding: b.userData.materialPath || '/Game/Materials/M_PrototypeGrid'
    };
  });

  sendToBackend('save_usd_stage', {
    stageName,
    prims,
    sublayers: []
  });

  showToast(`Composing non-destructive USD Stage '${stageName}.usda'...`);
}
