/**
 * @file sceneBridge.js
 * @brief Coordinates project serialization, OBJ/USD exports, raytraced AO baking, and toolchain queries.
 */

// ============================================================================
// SCENE BRIDGE (Clipboard Export, Save/Load, OBJ Export, USD Stage, AO)
// ============================================================================

import { blocks, selectedBlock, createBlock, refreshOutliner } from './sceneManager.js';
import { scene } from './engine.js';
import { sendToBackend, showToast } from './ipcBridge.js';
import { initHistory, serializeScene, deserializeScene } from './historyManager.js';

/**
 * @brief Serializes selected or all scene blocks to the system clipboard via backend IPC.
 */
export function copyToClipboard() {
  if (blocks.length === 0) {
    alert('No blocks in scene to copy!');
    return;
  }

  const targetList = selectedBlock ? [selectedBlock] : blocks;
  const payload = targetList.map((b) => ({
    name: b.userData.name,
    shapeType: b.userData.shapeType,
    mobility: b.userData.mobility || 'Static',
    intensity: b.userData.intensity || 5000,
    radius: b.userData.radius || 1000,
    color: b.userData.color || '#ffffff',
    sizeX: b.userData.sizeX,
    sizeY: b.userData.sizeY,
    sizeZ: b.userData.sizeZ,
    posX: b.position.x,
    posY: b.position.z, // Three.js Z -> Engine Y
    posZ: b.position.y, // Three.js Y -> Engine Z
    rotZ: THREE.MathUtils.radToDeg(b.rotation.y)
  }));

  sendToBackend('copy_clipboard', { data: payload });
}

/**
 * @brief Saves the active project layout to disk as a JSON manifest via backend IPC.
 */
export function saveProject() {
  const sceneSnapshot = serializeScene();
  const data = {
    formatVersion: 2,
    engineVersion: '1.0.0',
    savedAt: new Date().toISOString(),
    ...sceneSnapshot
  };
  sendToBackend('save_project', { projectData: JSON.stringify(data, null, 2) });
}

/**
 * @brief Sends an IPC request to prompt the user to load a saved project from disk.
 */
export function loadProject() {
  sendToBackend('load_project');
}

/**
 * @brief Clears existing blocks and restores scene geometry from a loaded project manifest.
 * @param {Object} data Parsed project data object containing the block list.
 */
export function loadProjectFromData(data) {
  if (!data) return;
  if (data.formatVersion >= 2 || (data.blocks && data.blocks.some(b => b.components || b.csgData || b.rotX !== undefined))) {
    deserializeScene(data);
  } else {
    // Legacy format compatibility: convert old block schema and restore
    const converted = {
      selectedName: null,
      blocks: (data.blocks || []).map(b => ({
        name: b.name,
        shapeType: b.shapeType || 'box',
        mobility: b.mobility || 'Static',
        intensity: b.intensity || 5000,
        radius: b.radius || 600,
        color: b.color || '#ffe6a3',
        sizeX: b.sizeX || 100,
        sizeY: b.sizeY || 100,
        sizeZ: b.sizeZ || 100,
        posX: b.posX || 0,
        posY: b.posZ !== undefined ? b.posZ : (b.posY || 0),
        posZ: b.posZ !== undefined ? b.posY : (b.posZ || 0),
        rotX: 0,
        rotY: b.rotZ || 0,
        rotZ: 0,
        customMesh: false,
        csgData: null,
        aoColors: null,
        components: []
      }))
    };
    deserializeScene(converted);
  }
  refreshOutliner();
  initHistory();
}

/**
 * @brief Collects world triangles and vertex normals and submits an AO bake job to the C# SIMD worker.
 */
export function requestBakeAo() {
  if (blocks.length === 0) return;
  showToast('Baking Raytraced AO in C# (.NET 10 SIMD)...');

  const allTriangles = [];
  blocks.forEach((b) => {
    b.updateMatrixWorld(true);
    const geom = b.geometry;
    if (!geom || !geom.attributes.position) return;
    const posAttr = geom.attributes.position;
    const worldMat = b.matrixWorld;

    const v0 = new THREE.Vector3();
    const v1 = new THREE.Vector3();
    const v2 = new THREE.Vector3();

    for (let i = 0; i < posAttr.count; i += 3) {
      v0.fromBufferAttribute(posAttr, i).applyMatrix4(worldMat);
      v1.fromBufferAttribute(posAttr, i + 1).applyMatrix4(worldMat);
      v2.fromBufferAttribute(posAttr, i + 2).applyMatrix4(worldMat);
      allTriangles.push([v0.x, v0.y, v0.z, v1.x, v1.y, v1.z, v2.x, v2.y, v2.z]);
    }
  });

  blocks.forEach((b) => {
    b.updateMatrixWorld(true);
    const geom = b.geometry;
    if (!geom || !geom.attributes.position || !geom.attributes.normal) return;

    const posAttr = geom.attributes.position;
    const normAttr = geom.attributes.normal;
    const worldMat = b.matrixWorld;
    const normMat = new THREE.Matrix3().getNormalMatrix(worldMat);

    const worldPositions = [];
    const worldNormals = [];

    const v = new THREE.Vector3();
    const n = new THREE.Vector3();

    for (let i = 0; i < posAttr.count; i++) {
      v.fromBufferAttribute(posAttr, i).applyMatrix4(worldMat);
      n.fromBufferAttribute(normAttr, i).applyMatrix3(normMat).normalize();
      worldPositions.push(v.x, v.y, v.z);
      worldNormals.push(n.x, n.y, n.z);
    }

    sendToBackend('bake_ao', {
      targetId: b.userData.name,
      triangles: allTriangles,
      targetMesh: { positions: worldPositions, normals: worldNormals }
    });
  });
}

/**
 * @brief Applies baked per-vertex ambient occlusion color data to a named target block mesh.
 * @param {string} targetName Unique identifier name of the target block.
 * @param {Array<number>} colors Array of RGB floating-point vertex colors.
 */
export function applyAoColors(targetName, colors) {
  const mesh = blocks.find((b) => b.userData.name === targetName);
  if (!mesh || !mesh.geometry) return;
  mesh.geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
  if (mesh.material) {
    mesh.material.vertexColors = true;
    mesh.material.needsUpdate = true;
  }
}

/**
 * @brief Exports selected or all scene blocks as a Wavefront .OBJ file in metric Z-Up coordinates.
 */
export function exportToObj() {
  const scope = document.getElementById('export-scope')?.value || 'all';
  const pivotMode = document.getElementById('export-pivot')?.value || 'world';
  const targetList = (scope === 'selected' && selectedBlock) ? [selectedBlock] : blocks;

  if (targetList.length === 0) {
    alert('No geometry to export!');
    return;
  }

  let obj = '# Exported from Blockout Studio (.NET 10 & Photino)\n';
  obj += '# Coordinate system: Z-Up Centimeters (1 Unit = 1 cm)\n\n';

  let vertexOffset = 1;

  targetList.forEach((mesh) => {
    if (!mesh.geometry) return;
    mesh.updateMatrixWorld(true);
    const geom = mesh.geometry.clone();
    const posAttr = geom.attributes.position;
    const normAttr = geom.attributes.normal;
    const uvAttr = geom.attributes.uv;

    obj += `o ${mesh.userData.name}\n`;
    obj += `g ${mesh.userData.name}\n`;

    const worldMat = mesh.matrixWorld.clone();
    if (pivotMode === 'ZeroBase') {
      worldMat.setPosition(0, 0, 0);
    }
    const normalMat = new THREE.Matrix3().getNormalMatrix(worldMat);

    const v = new THREE.Vector3();
    const n = new THREE.Vector3();

    for (let i = 0; i < posAttr.count; i++) {
      v.fromBufferAttribute(posAttr, i).applyMatrix4(worldMat);
      obj += `v ${(-v.z).toFixed(4)} ${v.x.toFixed(4)} ${v.y.toFixed(4)}\n`;
    }

    if (uvAttr) {
      for (let i = 0; i < uvAttr.count; i++) {
        obj += `vt ${uvAttr.getX(i).toFixed(4)} ${uvAttr.getY(i).toFixed(4)}\n`;
      }
    }

    if (normAttr) {
      for (let i = 0; i < normAttr.count; i++) {
        n.fromBufferAttribute(normAttr, i).applyMatrix3(normalMat).normalize();
        obj += `vn ${(-n.z).toFixed(4)} ${n.x.toFixed(4)} ${n.y.toFixed(4)}\n`;
      }
    }

    for (let i = 0; i < posAttr.count; i += 3) {
      const i1 = vertexOffset + i;
      const i2 = vertexOffset + i + 1;
      const i3 = vertexOffset + i + 2;
      obj += `f ${i1}/${i1}/${i1} ${i2}/${i2}/${i2} ${i3}/${i3}/${i3}\n`;
    }

    vertexOffset += posAttr.count;
    obj += '\n';
  });

  const blob = new Blob([obj], { type: 'text/plain' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = (scope === 'selected' && selectedBlock)
    ? `${selectedBlock.userData.name}.obj`
    : 'Level_Blockout.obj';
  a.click();
  URL.revokeObjectURL(url);
}

/**
 * @brief Exports selected or all scene blocks to a Universal Scene Description (.usda) ASCII stage.
 */
export function exportToUsd() {
  const scope = document.getElementById('export-scope')?.value || 'all';
  const targetList = (scope === 'selected' && selectedBlock) ? [selectedBlock] : blocks;

  if (targetList.length === 0) {
    alert('No geometry in scene to export as USD stage!');
    return;
  }

  showToast('Generating Universal Scene Description (.usda) stage...');

  const payload = targetList.map((mesh) => {
    mesh.updateMatrixWorld(true);
    const geom = mesh.geometry;
    if (!geom || !geom.attributes.position) return null;

    const posAttr = geom.attributes.position;
    const normAttr = geom.attributes.normal;
    const uvAttr = geom.attributes.uv;
    const worldMat = mesh.matrixWorld;

    const v = new THREE.Vector3();
    const n = new THREE.Vector3();
    const normalMat = new THREE.Matrix3().getNormalMatrix(worldMat);

    const worldPos = [];
    const worldNorms = [];
    const uvs = [];

    for (let i = 0; i < posAttr.count; i++) {
      v.fromBufferAttribute(posAttr, i).applyMatrix4(worldMat);
      worldPos.push(v.x, v.y, v.z);

      if (normAttr) {
        n.fromBufferAttribute(normAttr, i).applyMatrix3(normalMat).normalize();
        worldNorms.push(n.x, n.y, n.z);
      }
      if (uvAttr) {
        uvs.push(uvAttr.getX(i), uvAttr.getY(i));
      }
    }

    return {
      name: mesh.userData.name,
      positions: worldPos,
      normals: worldNorms.length ? worldNorms : null,
      uvs: uvs.length ? uvs : null,
      indices: geom.index ? Array.from(geom.index.array) : null
    };
  }).filter(Boolean);

  sendToBackend('export_usd', { blocks: payload });
}

/**
 * @brief Requests toolchain discovery status from the C# backend.
 */
export function queryToolchains() {
  sendToBackend('get_toolchains');
}

/**
 * @brief Requests native C++ SIMD library linkage and capability status from the C# backend.
 */
export function queryNativeStatus() {
  sendToBackend('get_native_status');
}
