// ============================================================================
// HOLE CUTTER TOOL (C# CSG Boolean Subtraction Engine)
// ============================================================================

import { selectedBlock } from '../sceneManager.js';
import { sendToBackend, showToast } from '../ipcBridge.js';

export function updateHoleUi() {
  const shape = document.getElementById('hole-shape')?.value;
  const w = document.getElementById('hole-w');
  const h = document.getElementById('hole-h');
  if (!w || !h) return;

  if (shape === 'door') {
    w.value = 120;
    h.value = 220;
  } else if (shape === 'window') {
    w.value = 120;
    h.value = 100;
  } else if (shape === 'circle') {
    w.value = 100;
    h.value = 100;
  }
}

export function punchHoleOnSelected() {
  if (!selectedBlock) {
    alert('Please select a block to punch a hole through.');
    return;
  }

  const shape = document.getElementById('hole-shape')?.value || 'door';
  const hw = parseFloat(document.getElementById('hole-w')?.value) || 120;
  const hh = parseFloat(document.getElementById('hole-h')?.value) || 220;
  const hd = parseFloat(document.getElementById('hole-depth')?.value) || 100;

  let cutterGeo;
  if (shape === 'circle') {
    cutterGeo = new THREE.CylinderGeometry(hw / 2, hw / 2, hd * 2, 24);
    cutterGeo.rotateX(Math.PI / 2);
  } else {
    cutterGeo = new THREE.BoxGeometry(hw, hh, hd * 2);
  }

  cutterGeo.translate(0, (shape === 'door') ? hh / 2 : selectedBlock.userData.sizeZ / 2, 0);

  const subGeom = selectedBlock.geometry.clone();
  const cutGeom = cutterGeo.toNonIndexed();

  const subjectData = {
    positions: Array.from(subGeom.attributes.position.array),
    normals: subGeom.attributes.normal ? Array.from(subGeom.attributes.normal.array) : null,
    uvs: subGeom.attributes.uv ? Array.from(subGeom.attributes.uv.array) : null
  };

  const cutterData = {
    positions: Array.from(cutGeom.attributes.position.array),
    normals: cutGeom.attributes.normal ? Array.from(cutGeom.attributes.normal.array) : null,
    uvs: cutGeom.attributes.uv ? Array.from(cutGeom.attributes.uv.array) : null
  };

  showToast('Calculating CSG boolean subtraction in C#...');
  sendToBackend('csg_cut', {
    targetId: selectedBlock.userData.name,
    subject: subjectData,
    cutter: cutterData
  });
}

export function applyCsgGeometry(mesh, positions, normals, uvs, indices) {
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geo.setAttribute('normal', new THREE.Float32BufferAttribute(normals, 3));
  geo.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
  if (indices && indices.length > 0) {
    geo.setIndex(indices);
  }

  mesh.geometry.dispose();
  mesh.geometry = geo;
  mesh.userData.customMesh = true;
  mesh.userData.csgData = { positions, normals, uvs, indices };

  if (mesh.children[0]) {
    mesh.children[0].geometry.dispose();
    mesh.children[0].geometry = new THREE.EdgesGeometry(geo, 25);
  }
}

