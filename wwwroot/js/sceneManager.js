// ============================================================================
// SCENE MANAGER (Blocks, Geometry Generation, Selection & Outliner)
// ============================================================================

import { scene, gridTexture, mannequinGroup, resetDummyTransform, toggleMannequin } from './engine.js';
import { transformControl, snapSize } from './transformGizmo.js';
import { recordState } from './historyManager.js';
import { applyCsgGeometry } from './tools/holeTool.js';
import {
  ensureEntityComponents, getComponent, addComponent, removeComponent,
  deserializeEntityComponents
} from './ecs/entity.js';
import { createComponentByType } from './ecs/components.js';

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

  ensureEntityComponents(mesh);

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

  if (data.components && data.components.length > 0) {
    deserializeEntityComponents(mesh, data.components);
  } else {
    ensureEntityComponents(mesh);
  }

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
  if (selectedBlock && selectedBlock !== mannequinGroup && selectedBlock.children[0] && selectedBlock.children[0].isLineSegments) {
    selectedBlock.children[0].material.color.setHex(0x181a20);
    if (selectedBlock.material && selectedBlock.material.emissive) {
      selectedBlock.material.emissive.setHex(0x000000);
    }
  }

  selectedBlock = mesh;

  if (mesh) {
    if (mesh !== mannequinGroup && mesh.children[0] && mesh.children[0].isLineSegments) {
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
  if (!selectedBlock) {
    const container = document.getElementById('component-stack');
    if (container) container.innerHTML = '<div style="color:var(--text-muted); font-size:11px; padding:4px 0;">No object selected</div>';
    return;
  }
  const el = (id) => document.getElementById(id);
  if (el('prop-name')) el('prop-name').value = selectedBlock.userData.name;
  if (el('dim-x')) el('dim-x').value = selectedBlock.userData.sizeX;
  if (el('dim-y')) el('dim-y').value = selectedBlock.userData.sizeY;
  if (el('dim-z')) el('dim-z').value = selectedBlock.userData.sizeZ;
  if (el('pos-x')) el('pos-x').value = Math.round(selectedBlock.position.x);
  if (el('pos-y')) el('pos-y').value = Math.round(selectedBlock.position.z);
  if (el('pos-z')) el('pos-z').value = Math.round(selectedBlock.position.y);
  if (el('rot-z')) el('rot-z').value = Math.round(THREE.MathUtils.radToDeg(selectedBlock.rotation.y));

  if (selectedBlock === mannequinGroup) {
    const container = document.getElementById('component-stack');
    if (container) {
      container.innerHTML = `
        <div class="component-card">
          <div class="component-header">
            <span class="component-title">🧍 180cm Reference Dummy</span>
          </div>
          <div class="component-body">
            <div style="font-size:11px; color:#cbd5e1; margin-bottom:8px; line-height:1.4;">
              Standard human scale reference (180 cm height, 60 cm width, 30 cm depth). Select Move or Rotate to check spatial clearance across doors, ramps, and stairs.
            </div>
            <button type="button" class="preset-btn" style="width:100%; justify-content:center; gap:6px;" onclick="window.resetDummyTransform()">
              <span>↺ Reset Dummy to Origin</span>
            </button>
          </div>
        </div>
      `;
    }
    return;
  }

  renderComponentStack(selectedBlock);
}

export function renderComponentStack(entity) {
  const container = document.getElementById('component-stack');
  if (!container) return;
  if (!entity || !entity.userData || !Array.isArray(entity.userData.components)) {
    container.innerHTML = '<div style="color:var(--text-muted); font-size:11px; padding:4px 0;">No object selected</div>';
    return;
  }

  const comps = entity.userData.components;
  let html = '';

  for (const comp of comps) {
    if (comp.type === 'Transform') continue;

    let icon = '🧩';
    let title = comp.type;
    let body = '';

    if (comp.type === 'Collider') {
      icon = '📐';
      title = 'Collider (Physics Boundary)';
      body = `
        <div class="comp-row">
          <span class="comp-lbl">Type</span>
          <select class="comp-input" onchange="updateComponentField('Collider', 'colliderType', this.value)">
            <option value="box" ${comp.colliderType === 'box' ? 'selected' : ''}>Box AABB</option>
            <option value="sphere" ${comp.colliderType === 'sphere' ? 'selected' : ''}>Sphere</option>
            <option value="capsule" ${comp.colliderType === 'capsule' ? 'selected' : ''}>Capsule (180cm)</option>
          </select>
        </div>
        <div class="comp-row">
          <span class="comp-lbl">Trigger</span>
          <label style="display:flex; align-items:center; gap:4px; font-size:11px; color:#cbd5e1; cursor:pointer;">
            <input type="checkbox" ${comp.isTrigger ? 'checked' : ''} onchange="updateComponentField('Collider', 'isTrigger', this.checked)">
            Trigger Only
          </label>
        </div>
      `;
    } else if (comp.type === 'Rigidbody') {
      icon = '⚖️';
      title = 'Rigidbody (Dynamics & Gravity)';
      body = `
        <div class="comp-row">
          <span class="comp-lbl">Mobility</span>
          <select class="comp-input" onchange="updateComponentField('Rigidbody', 'mobility', this.value)">
            <option value="Static" ${comp.mobility === 'Static' ? 'selected' : ''}>Static (Immovable)</option>
            <option value="Kinematic" ${comp.mobility === 'Kinematic' ? 'selected' : ''}>Kinematic</option>
            <option value="Dynamic" ${comp.mobility === 'Dynamic' ? 'selected' : ''}>Dynamic (Simulated)</option>
          </select>
        </div>
        <div class="comp-row">
          <span class="comp-lbl">Mass (kg)</span>
          <input type="number" class="comp-input" value="${comp.mass}" min="1" step="5" onchange="updateComponentField('Rigidbody', 'mass', parseFloat(this.value)||50)">
        </div>
        <div class="comp-row">
          <span class="comp-lbl">Gravity</span>
          <label style="display:flex; align-items:center; gap:4px; font-size:11px; color:#cbd5e1; cursor:pointer;">
            <input type="checkbox" ${comp.useGravity ? 'checked' : ''} onchange="updateComponentField('Rigidbody', 'useGravity', this.checked)">
            Use Gravity (-980 cm/s²)
          </label>
        </div>
      `;
    } else if (comp.type === 'CharacterController') {
      icon = '🏃';
      title = 'Character Controller (UE5 Mannequin)';
      body = `
        <div class="comp-row">
          <span class="comp-lbl">Walk Speed</span>
          <input type="number" class="comp-input" value="${comp.walkSpeed}" step="50" onchange="updateComponentField('CharacterController', 'walkSpeed', parseFloat(this.value)||450)">
        </div>
        <div class="comp-row">
          <span class="comp-lbl">Sprint Speed</span>
          <input type="number" class="comp-input" value="${comp.sprintSpeed}" step="50" onchange="updateComponentField('CharacterController', 'sprintSpeed', parseFloat(this.value)||750)">
        </div>
        <div class="comp-row">
          <span class="comp-lbl">Jump Vel</span>
          <input type="number" class="comp-input" value="${comp.jumpVelocity}" step="50" onchange="updateComponentField('CharacterController', 'jumpVelocity', parseFloat(this.value)||600)">
        </div>
        <div class="comp-row">
          <span class="comp-lbl">Max Step H</span>
          <input type="number" class="comp-input" value="${comp.stepHeight}" step="5" onchange="updateComponentField('CharacterController', 'stepHeight', parseFloat(this.value)||45)">
        </div>
      `;
    } else if (comp.type === 'Script') {
      icon = '📜';
      title = 'Script (Gameplay Logic)';
      body = `
        <div class="comp-row">
          <span class="comp-lbl">Script</span>
          <select class="comp-input" onchange="updateComponentField('Script', 'scriptPath', this.value)">
            <option value="Rotator.js" ${comp.scriptPath === 'Rotator.js' ? 'selected' : ''}>Rotator.js</option>
            <option value="DoorTrigger.js" ${comp.scriptPath === 'DoorTrigger.js' ? 'selected' : ''}>DoorTrigger.js</option>
            <option value="PlayerMovement.js" ${comp.scriptPath === 'PlayerMovement.js' ? 'selected' : ''}>PlayerMovement.js</option>
          </select>
        </div>
        <div class="comp-row">
          <span class="comp-lbl">Speed (deg/s)</span>
          <input type="number" class="comp-input" value="${comp.properties ? comp.properties.speed || 45 : 45}" step="15" onchange="updateScriptProp('speed', parseFloat(this.value)||45)">
        </div>
      `;
    } else if (comp.type === 'AudioSource') {
      icon = '🔊';
      title = 'Audio Source (3D Spatial)';
      body = `
        <div class="comp-row">
          <span class="comp-lbl">Clip</span>
          <select class="comp-input" onchange="updateComponentField('AudioSource', 'clipPath', this.value)">
            <option value="chime" ${comp.clipPath === 'chime' ? 'selected' : ''}>Chime (Synth Harmonic)</option>
            <option value="jump" ${comp.clipPath === 'jump' ? 'selected' : ''}>Jump Sweep</option>
            <option value="footstep" ${comp.clipPath === 'footstep' ? 'selected' : ''}>Footstep Impact</option>
            <option value="impact" ${comp.clipPath === 'impact' ? 'selected' : ''}>Impact Thud</option>
          </select>
        </div>
        <div class="comp-row">
          <span class="comp-lbl">Volume</span>
          <input type="range" min="0" max="1" step="0.05" value="${comp.volume}" style="width:100px;" onchange="updateComponentField('AudioSource', 'volume', parseFloat(this.value))">
        </div>
      `;
    } else if (comp.type === 'Mesh') {
      icon = '🧊';
      title = 'Mesh & Material';
      body = `
        <div class="comp-row">
          <span class="comp-lbl">Shape</span>
          <span style="font-size:11px; color:#94a3b8;">${comp.shapeType}</span>
        </div>
        <div class="comp-row">
          <span class="comp-lbl">Roughness</span>
          <input type="range" min="0" max="1" step="0.05" value="${comp.roughness}" style="width:100px;" onchange="updateComponentField('Mesh', 'roughness', parseFloat(this.value))">
        </div>
      `;
    }

    html += `
      <div class="component-card">
        <div class="component-header">
          <span class="component-title">${icon} ${title}</span>
          <span class="component-del" onclick="removeComponentFromSelected('${comp.type}')" title="Remove Component">✕</span>
        </div>
        <div class="component-body">
          ${body}
        </div>
      </div>
    `;
  }

  container.innerHTML = html;
}

export function addComponentToSelected(type) {
  if (!selectedBlock) return;
  const comp = createComponentByType(type);
  if (comp) {
    addComponent(selectedBlock, comp);
    syncInspectorFromSelected();
    recordState(`Add ${type} to ${selectedBlock.userData.name}`);
  }
}

export function removeComponentFromSelected(type) {
  if (!selectedBlock) return;
  if (removeComponent(selectedBlock, type)) {
    syncInspectorFromSelected();
    recordState(`Remove ${type} from ${selectedBlock.userData.name}`);
  }
}

export function updateComponentField(type, field, value) {
  if (!selectedBlock) return;
  const comp = getComponent(selectedBlock, type);
  if (comp) {
    comp[field] = value;
    recordState(`Update ${type}.${field}`);
  }
}

export function updateScriptProp(propName, value) {
  if (!selectedBlock) return;
  const comp = getComponent(selectedBlock, 'Script');
  if (comp) {
    if (!comp.properties) comp.properties = {};
    comp.properties[propName] = value;
    recordState(`Update Script.${propName}`);
  }
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
  if (selectedBlock !== mannequinGroup) {
    recordState('Move ' + selectedBlock.userData.name);
  }
}

export function duplicateSelected() {
  if (!selectedBlock || selectedBlock === mannequinGroup) return;
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
  if (selectedBlock === mannequinGroup) {
    toggleMannequin();
    selectBlock(null);
    return;
  }
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
  const dummyCount = (mannequinGroup && mannequinGroup.visible) ? 1 : 0;
  if (countEl) countEl.textContent = `${blocks.length + dummyCount} item(s)`;

  if (mannequinGroup && mannequinGroup.visible) {
    const dummyDiv = document.createElement('div');
    dummyDiv.className = 'outliner-item' + (selectedBlock === mannequinGroup ? ' selected' : '');
    dummyDiv.innerHTML = `<span>🧍 180cm Dummy</span><span style="opacity:0.75; font-size:10px;">60&times;30&times;180</span>`;
    dummyDiv.onclick = () => selectBlock(mannequinGroup);
    container.appendChild(dummyDiv);
  }

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

window.resetDummyTransform = () => {
  resetDummyTransform();
  syncInspectorFromSelected();
};

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

