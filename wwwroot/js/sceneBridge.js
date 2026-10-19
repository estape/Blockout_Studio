// ============================================================================
// SCENE BRIDGE (Clipboard Export, Save/Load, OBJ Export, USD Stage, AO)
// ============================================================================

import { blocks, selectedBlock, createBlock, refreshOutliner } from './sceneManager.js';
import { scene } from './engine.js';
import { sendToBackend, showToast } from './ipcBridge.js';
import { initHistory } from './historyManager.js';

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

export function saveProject() {
  const data = {
    version: '1.0',
    blocks: blocks.map((b) => ({
      name: b.userData.name,
      shapeType: b.userData.shapeType,
      mobility: b.userData.mobility,
      intensity: b.userData.intensity,
      radius: b.userData.radius,
      color: b.userData.color,
      sizeX: b.userData.sizeX,
      sizeY: b.userData.sizeY,
      sizeZ: b.userData.sizeZ,
      posX: b.position.x,
      posY: b.position.z,
      posZ: b.position.y,
      rotZ: b.rotation.y
    }))
  };
  sendToBackend('save_project', { projectData: JSON.stringify(data, null, 2) });
}

export function loadProject() {
  sendToBackend('load_project');
}

export function loadProjectFromData(data) {
  if (!data.blocks) return;
  while (blocks.length > 0) {
    const b = blocks.pop();
    scene.remove(b);
  }
  data.blocks.forEach((b) => {
    const mesh = createBlock(
      b.name.replace(/_\d+$/, ''),
      b.shapeType,
      b.sizeX, b.sizeY, b.sizeZ,
      b.posX, b.posY, b.posZ,
      false
    );
    mesh.rotation.y = b.rotZ || 0;
  });
  refreshOutliner();
  initHistory();
}

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

export function applyAoColors(targetName, colors) {
  const mesh = blocks.find((b) => b.userData.name === targetName);
  if (!mesh || !mesh.geometry) return;
  mesh.geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
  if (mesh.material) {
    mesh.material.vertexColors = true;
    mesh.material.needsUpdate = true;
  }
}

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

export function queryToolchains() {
  sendToBackend('get_toolchains');
}

export function queryNativeStatus() {
  sendToBackend('get_native_status');
}

