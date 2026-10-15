// ============================================================================
// SCENE MANAGER (Blocks, Geometry Generation, Selection & Outliner)
// ============================================================================

import { scene, gridTexture } from './engine.js';
import { transformControl, snapSize } from './transformGizmo.js';
import { recordState } from './historyManager.js';
import { applyCsgGeometry } from './tools/holeTool.js';

export const blocks = [];
export let selectedBlock = null;
let blockCounter = 1;

export function buildBlockGeometry(shapeType, sizeX, sizeY, sizeZ) {
  let geo;
  if (shapeType === 'ramp') {
    geo = new THREE.BufferGeometry();
    const hw = sizeX / 2; const hl = sizeY / 2; const h = sizeZ;
    const positions = new Float32Array([
      -hw, 0, -hl,   hw, 0, -hl,   hw, 0,  hl,
      -hw, 0, -hl,   hw, 0,  hl,  -hw, 0,  hl,
      -hw, 0, -hl,  -hw, h, -hl,   hw, h, -hl,
      -hw, 0, -hl,   hw, h, -hl,   hw, 0, -hl,
      -hw, h, -hl,  -hw, 0,  hl,   hw, 0,  hl,
      -hw, h, -hl,   hw, 0,  hl,   hw, h, -hl,
      -hw, 0, -hl,  -hw, 0,  hl,  -hw, h, -hl,
       hw, 0, -hl,   hw, h, -hl,   hw, 0,  hl
    ]);
    geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geo.computeVertexNormals();
  } else if (shapeType === 'cylinder') {
    geo = new THREE.CylinderGeometry(sizeX / 2, sizeX / 2, sizeZ, 24);
    geo.translate(0, sizeZ / 2, 0);
  } else if (shapeType === 'sphere') {
    const radius = sizeX / 2;
    geo = new THREE.SphereGeometry(radius, 24, 16);
    geo.translate(0, radius, 0);
  } else {
    // Standard Box
    geo = new THREE.BoxGeometry(sizeX, sizeZ, sizeY);
    geo.translate(0, sizeZ / 2, 0);
  }

  const nonIndexed = geo.index ? geo.toNonIndexed() : geo;
  nonIndexed.computeVertexNormals();
  const posAttr = nonIndexed.attributes.position;
  const normAttr = nonIndexed.attributes.normal;
  const uvs = new Float32Array(posAttr.count * 2);

  for (let i = 0; i < posAttr.count; i++) {
    const vx = posAttr.getX(i);
    const vy = posAttr.getY(i);
    const vz = posAttr.getZ(i);
    const nx = Math.abs(normAttr.getX(i));
    const ny = Math.abs(normAttr.getY(i));
    const nz = Math.abs(normAttr.getZ(i));

    if (ny >= nx && ny >= nz) {
      uvs[i * 2] = vx / 100; uvs[i * 2 + 1] = vz / 100;
    } else if (nx >= ny && nx >= nz) {
      uvs[i * 2] = vz / 100; uvs[i * 2 + 1] = vy / 100;
    } else {
      uvs[i * 2] = vx / 100; uvs[i * 2 + 1] = vy / 100;
    }
  }
  nonIndexed.setAttribute('uv', new THREE.BufferAttribute(uvs, 2));
  return nonIndexed;
}

export function createBlock(name, shapeType, sizeX, sizeY, sizeZ, posX = 0, posY = 0, posZ = 0, shouldRecord = true) {
  const geo = buildBlockGeometry(shapeType, sizeX, sizeY, sizeZ);
  const mat = new THREE.MeshStandardMaterial({
    map: gridTexture,
    roughness: 0.7,
    metalness: 0.1,
    vertexColors: false,
    emissive: new THREE.Color(0x000000)
  });
  const mesh = new THREE.Mesh(geo, mat);

  // Wireframe edge helper
  const edges = new THREE.LineSegments(
    new THREE.EdgesGeometry(geo, 25),
    new THREE.LineBasicMaterial({ color: 0x181a20, linewidth: 1.5 })
  );
  mesh.add(edges);

  mesh.position.set(posX, posZ, posY);
  mesh.userData = {
    name: `${name}_${blockCounter++}`,
    shapeType,
    sizeX: Math.max(1, Math.round(sizeX)),
    sizeY: Math.max(1, Math.round(sizeY)),
    sizeZ: Math.max(1, Math.round(sizeZ)),
    customMesh: false
  };

  scene.add(mesh);
  blocks.push(mesh);
  selectBlock(mesh);
  refreshOutliner();
  if (shouldRecord) {
    recordState('Create ' + mesh.userData.name);
  }
  return mesh;
}

export function restoreBlockFromData(data) {
  const geo = buildBlockGeometry(data.shapeType, data.sizeX, data.sizeY, data.sizeZ);
  const mat = new THREE.MeshStandardMaterial({
    map: gridTexture,
    roughness: 0.7,
    metalness: 0.1,
    vertexColors: false,
    emissive: new THREE.Color(0x000000)
  });
  const mesh = new THREE.Mesh(geo, mat);

  const edges = new THREE.LineSegments(
    new THREE.EdgesGeometry(geo, 25),
    new THREE.LineBasicMaterial({ color: 0x181a20, linewidth: 1.5 })
  );
  mesh.add(edges);

  mesh.position.set(data.posX, data.posY, data.posZ);
  mesh.rotation.set(data.rotX || 0, data.rotY || 0, data.rotZ || 0);

  mesh.userData = {
    name: data.name,
    shapeType: data.shapeType,
    sizeX: data.sizeX,
    sizeY: data.sizeY,
    sizeZ: data.sizeZ,
    customMesh: data.customMesh || false,
    csgData: data.csgData || null
  };

  if (data.customMesh && data.csgData) {
    applyCsgGeometry(mesh, data.csgData.positions, data.csgData.normals, data.csgData.uvs, data.csgData.indices);
    mesh.userData.csgData = data.csgData;
  }

  if (data.aoColors) {
    mesh.geometry.setAttribute('color', new THREE.Float32BufferAttribute(data.aoColors, 3));
    mesh.material.vertexColors = true;
    mesh.material.needsUpdate = true;
  }

  const m = data.name.match(/_(\d+)$/);
  if (m) {
    const num = parseInt(m[1], 10);
    if (!isNaN(num) && num >= blockCounter) {
      blockCounter = num + 1;
    }
  }

  scene.add(mesh);
  blocks.push(mesh);
  return mesh;
}

export function clearSceneBlocks() {
  transformControl.detach();
  while (blocks.length > 0) {
    const b = blocks.pop();
    scene.remove(b);
    if (b.geometry) b.geometry.dispose();
    if (b.material) {
      if (Array.isArray(b.material)) b.material.forEach((m) => m.dispose());
      else b.material.dispose();
    }
    if (b.children) {
      b.children.forEach((c) => {
        if (c.geometry) c.geometry.dispose();
        if (c.material) {
          if (Array.isArray(c.material)) c.material.forEach((m) => m.dispose());
          else c.material.dispose();
        }
      });
    }
  }
  selectedBlock = null;
}

export function rebuildBlockMesh(mesh) {
  if (mesh.userData.customMesh) return;
  const { shapeType, sizeX, sizeY, sizeZ } = mesh.userData;
  mesh.geometry.dispose();
  mesh.geometry = buildBlockGeometry(shapeType, sizeX, sizeY, sizeZ);
  if (mesh.children[0]) {
    mesh.children[0].geometry.dispose();
    mesh.children[0].geometry = new THREE.EdgesGeometry(mesh.geometry, 25);
  }
}

export function selectBlock(mesh) {
  if (selectedBlock && selectedBlock.children[0]) {
    selectedBlock.children[0].material.color.setHex(0x181a20);
    if (selectedBlock.material && selectedBlock.material.emissive) {
      selectedBlock.material.emissive.setHex(0x000000);
    }
  }

  selectedBlock = mesh;

  if (mesh) {
    if (mesh.children[0]) {
      mesh.children[0].material.color.setHex(0xf58220); // Bright orange edge outline
    }
    if (mesh.material && mesh.material.emissive) {
      mesh.material.emissive.setHex(0x241708); // Subtle warm selection glow
    }

    transformControl.attach(mesh);
    transformControl.setSpace('world');
    syncInspectorFromSelected();
  } else {
    transformControl.detach();
  }

  refreshOutliner();
}

export function syncInspectorFromSelected() {
  if (!selectedBlock) return;
  const el = (id) => document.getElementById(id);
  if (el('prop-name')) el('prop-name').value = selectedBlock.userData.name;
  if (el('dim-x')) el('dim-x').value = selectedBlock.userData.sizeX;
  if (el('dim-y')) el('dim-y').value = selectedBlock.userData.sizeY;
  if (el('dim-z')) el('dim-z').value = selectedBlock.userData.sizeZ;
  if (el('pos-x')) el('pos-x').value = Math.round(selectedBlock.position.x);
  if (el('pos-y')) el('pos-y').value = Math.round(selectedBlock.position.z);
  if (el('pos-z')) el('pos-z').value = Math.round(selectedBlock.position.y);
  if (el('rot-z')) el('rot-z').value = Math.round(THREE.MathUtils.radToDeg(selectedBlock.rotation.y));
}

export function applyInspectorChanges() {
  if (!selectedBlock) return;
  const el = (id) => document.getElementById(id);
  selectedBlock.userData.name = el('prop-name').value || 'SM_Block';
  selectedBlock.userData.sizeX = Math.max(1, parseFloat(el('dim-x').value) || 100);
  selectedBlock.userData.sizeY = Math.max(1, parseFloat(el('dim-y').value) || 100);
  selectedBlock.userData.sizeZ = Math.max(1, parseFloat(el('dim-z').value) || 100);
  rebuildBlockMesh(selectedBlock);
  refreshOutliner();
  recordState('Modify ' + selectedBlock.userData.name);
}

export function applyPositionChanges() {
  if (!selectedBlock) return;
  const el = (id) => document.getElementById(id);
  selectedBlock.position.x = parseFloat(el('pos-x').value) || 0;
  selectedBlock.position.z = parseFloat(el('pos-y').value) || 0;
  selectedBlock.position.y = parseFloat(el('pos-z').value) || 0;
  const deg = parseFloat(el('rot-z').value) || 0;
  selectedBlock.rotation.y = THREE.MathUtils.degToRad(deg);
  recordState('Move ' + selectedBlock.userData.name);
}

export function duplicateSelected() {
  if (!selectedBlock) return;
  const u = selectedBlock.userData;
  const copy = createBlock(
    u.name.replace(/_\d+$/, ''),
    u.shapeType,
    u.sizeX, u.sizeY, u.sizeZ,
    selectedBlock.position.x + snapSize,
    selectedBlock.position.z + snapSize,
    selectedBlock.position.y,
    false
  );
  copy.rotation.copy(selectedBlock.rotation);
  selectBlock(copy);
  recordState('Duplicate ' + u.name);
}

export function deleteSelected() {
  if (!selectedBlock) return;
  const name = selectedBlock.userData.name;
  transformControl.detach();
  scene.remove(selectedBlock);
  const idx = blocks.indexOf(selectedBlock);
  if (idx > -1) blocks.splice(idx, 1);
  selectedBlock = blocks.length ? blocks[blocks.length - 1] : null;
  if (selectedBlock) selectBlock(selectedBlock);
  refreshOutliner();
  recordState('Delete ' + name);
}

export function refreshOutliner() {
  const container = document.getElementById('outliner');
  if (!container) return;
  container.innerHTML = '';
  const countEl = document.getElementById('block-count');
  if (countEl) countEl.textContent = `${blocks.length} item(s)`;

  blocks.forEach((b) => {
    const div = document.createElement('div');
    div.className = 'outliner-item' + (b === selectedBlock ? ' selected' : '');
    const isLight = b.userData.shapeType && b.userData.shapeType.includes('light');
    const icon = isLight ? '💡' : '📦';
    div.innerHTML = `<span>${icon} ${b.userData.name}</span><span style="opacity:0.75; font-size:10px;">${b.userData.sizeX}&times;${b.userData.sizeY}&times;${b.userData.sizeZ}</span>`;
    div.onclick = () => selectBlock(b);
    container.appendChild(div);
  });
}

export function spawnPreset(type) {
  if (type === 'wall') createBlock('SM_Wall', 'box', 400, 20, 300, 0, 0, 0);
  if (type === 'floor') createBlock('SM_Floor', 'box', 400, 400, 20, 0, 0, 0);
  if (type === 'pillar') createBlock('SM_Pillar', 'box', 50, 50, 300, 0, 0, 0);
  if (type === 'crate') createBlock('SM_CoverBox', 'box', 100, 100, 100, 0, 0, 0);
  if (type === 'platform') createBlock('SM_Platform', 'box', 200, 200, 100, 0, 0, 0);
  if (type === 'ramp') createBlock('SM_Ramp', 'ramp', 200, 300, 150, 0, 0, 0);
  if (type === 'cylinder') createBlock('SM_Cylinder', 'cylinder', 100, 100, 200, 0, 0, 0);
  if (type === 'sphere') createBlock('SM_Sphere', 'sphere', 100, 100, 100, 0, 0, 0);
}

