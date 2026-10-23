/**
 * @file blueprintManager.js
 * @brief Central controller orchestrating graph state, Roslyn compilation, IPC bridge and UI.
 */

import { sendToBackend, registerHandler } from '../ipcBridge.js';
import { createNodeInstance, arePinsCompatible, PinTypes } from './blueprintNodes.js';
import { BlueprintCanvas } from './blueprintCanvas.js';

export class BlueprintManager {
  constructor() {
    this.currentGraph = this.createEmptyGraph('BP_NewActor');
    this.availableBlueprints = [];
    this.selectedNodeId = null;
    this.selectedVariableId = null;
    this.isDirty = false;
    this.isPlaying = false;
    this.activeTab = 'viewport';

    this.initDOM();
    this.canvas = new BlueprintCanvas(this.canvasContainer, this);
    this.registerIpcHandlers();
    this.refreshBlueprintList();
  }

  initDOM() {
    this.viewEl = document.getElementById('blueprint-view');
    this.canvasContainer = document.getElementById('bp-canvas-container');
    this.compileBtn = document.getElementById('bp-btn-compile');
    this.compileBadge = document.getElementById('bp-compile-badge');
    this.saveBtn = document.getElementById('bp-btn-save');
    this.selectAssetEl = document.getElementById('bp-select-asset');
    this.newBtn = document.getElementById('bp-btn-new');
    this.csharpDrawer = document.getElementById('bp-csharp-drawer');
    this.csharpCodeEl = document.getElementById('bp-csharp-code');
    this.btnToggleCs = document.getElementById('bp-btn-toggle-cs');
    this.varsListEl = document.getElementById('bp-vars-list');
    this.btnAddVar = document.getElementById('bp-btn-add-var');

    // Bind UI actions
    if (this.compileBtn) this.compileBtn.addEventListener('click', () => this.compileBlueprint());
    if (this.saveBtn) this.saveBtn.addEventListener('click', () => this.saveBlueprint());
    if (this.newBtn) this.newBtn.addEventListener('click', () => this.promptNewBlueprint());
    if (this.selectAssetEl) this.selectAssetEl.addEventListener('change', (e) => this.loadBlueprint(e.target.value));

    if (this.btnToggleCs) {
      this.btnToggleCs.addEventListener('click', () => {
        const isHidden = this.csharpDrawer.style.display === 'none';
        this.csharpDrawer.style.display = isHidden ? 'flex' : 'none';
        if (isHidden) this.updateCSharpPreview();
      });
    }

    if (this.btnAddVar) {
      this.btnAddVar.addEventListener('click', () => this.addVariable());
    }
  }

  createEmptyGraph(name = 'BP_NewActor', type = 'Actor') {
    return {
      id: 'bp_' + Math.random().toString(36).substring(2, 9),
      name,
      type,
      nodes: [],
      connections: [],
      variables: []
    };
  }

  registerIpcHandlers() {
    registerHandler('list_blueprints_response', (data) => {
      if (data.success && Array.isArray(data.blueprints)) {
        this.availableBlueprints = data.blueprints;
        this.updateAssetDropdown();
      }
    });

    registerHandler('load_blueprint_response', (data) => {
      if (data.success && data.blueprint) {
        this.currentGraph = data.blueprint;
        this.isDirty = false;
        this.updateCompileBadge('clean');
        this.renderGraph();
        this.updateVariablesUI();
        this.updateCSharpPreview();
        if (window.showToast) window.showToast(`Loaded Blueprint '${this.currentGraph.name}'`);
      }
    });

    registerHandler('save_blueprint_response', (data) => {
      if (data.success) {
        this.isDirty = false;
        this.updateCompileBadge('clean');
        this.refreshBlueprintList();
        if (window.showToast) window.showToast(`Saved ${this.currentGraph.name}.bprint & emitted C#`);
      } else {
        if (window.showToast) window.showToast(`Save failed: ${data.message || 'Unknown error'}`, true);
      }
    });

    registerHandler('transpile_blueprint_response', (data) => {
      if (data.success && data.csharpCode) {
        if (this.csharpCodeEl) {
          this.csharpCodeEl.textContent = data.csharpCode;
        }
      }
    });

    registerHandler('compile_blueprint_response', (data) => {
      if (data.success) {
        this.updateCompileBadge('clean');
        if (window.showToast) window.showToast(`Roslyn Compilation Successful (${data.compilationTimeMs}ms)`);
      } else {
        this.updateCompileBadge('error');
        if (window.showToast) window.showToast(`Compile Error: ${data.message || 'Syntax/Semantic error'}`, true);
      }
    });
  }

  refreshBlueprintList() {
    sendToBackend({ action: 'list_blueprints' });
  }

  updateAssetDropdown() {
    if (!this.selectAssetEl) return;
    this.selectAssetEl.innerHTML = '';

    const defaultOpt = document.createElement('option');
    defaultOpt.value = '';
    defaultOpt.textContent = '-- Select Blueprint --';
    this.selectAssetEl.appendChild(defaultOpt);

    for (const bp of this.availableBlueprints) {
      const opt = document.createElement('option');
      opt.value = bp.name;
      opt.textContent = `${bp.name} (${bp.graphType})`;
      if (bp.name === this.currentGraph.name) {
        opt.selected = true;
      }
      this.selectAssetEl.appendChild(opt);
    }
  }

  promptNewBlueprint() {
    const name = prompt('Enter new Blueprint name:', `BP_Actor_${Math.floor(Math.random() * 900 + 100)}`);
    if (!name || !name.trim()) return;

    const cleanName = name.trim().replace(/[^a-zA-Z0-9_]/g, '_');
    this.currentGraph = this.createEmptyGraph(cleanName, 'Actor');

    // Add Starter Event BeginPlay and Tick nodes
    const bp1 = createNodeInstance('EventBeginPlay', 80, 80);
    const bp2 = createNodeInstance('EventTick', 80, 220);
    this.currentGraph.nodes.push(bp1, bp2);

    this.isDirty = true;
    this.updateCompileBadge('dirty');
    this.renderGraph();
    this.updateVariablesUI();
    this.saveBlueprint();
  }

  loadBlueprint(name) {
    if (!name) return;
    sendToBackend({ action: 'load_blueprint', name });
  }

  saveBlueprint() {
    sendToBackend({
      action: 'save_blueprint',
      graph: this.currentGraph
    });
  }

  compileBlueprint() {
    this.updateCompileBadge('compiling');
    // Save and compile in sequence
    this.saveBlueprint();
    sendToBackend({
      action: 'transpile_blueprint',
      graph: this.currentGraph
    });

    setTimeout(() => {
      if (this.csharpCodeEl && this.csharpCodeEl.textContent) {
        sendToBackend({
          action: 'compile_blueprint',
          csharpCode: this.csharpCodeEl.textContent
        });
      }
    }, 200);
  }

  updateCompileBadge(status) {
    if (!this.compileBadge) return;
    this.compileBadge.className = `bp-compile-badge ${status}`;
    if (status === 'clean') {
      this.compileBadge.textContent = '✓ Good';
    } else if (status === 'dirty') {
      this.compileBadge.textContent = '● Dirty';
    } else if (status === 'error') {
      this.compileBadge.textContent = '✕ Error';
    } else if (status === 'compiling') {
      this.compileBadge.textContent = '⟳ Compiling...';
    }
  }

  updateCSharpPreview() {
    sendToBackend({
      action: 'transpile_blueprint',
      graph: this.currentGraph
    });
  }

  onGraphModified() {
    this.isDirty = true;
    this.updateCompileBadge('dirty');
    this.updateCSharpPreview();
  }

  renderGraph() {
    this.canvas.renderGraph(this.currentGraph);
  }

  selectNode(nodeId) {
    this.selectedNodeId = nodeId;
    const allNodes = this.canvas.nodesContainer.querySelectorAll('.bp-node');
    for (const el of allNodes) {
      if (el.dataset.nodeId === nodeId) {
        el.classList.add('selected');
      } else {
        el.classList.remove('selected');
      }
    }
  }

  spawnNode(nodeType, worldX, worldY, customProps = {}, sourcePin = null) {
    const node = createNodeInstance(nodeType, worldX, worldY, customProps);
    this.currentGraph.nodes.push(node);

    // Auto-connect if spawned by pulling from a pin
    if (sourcePin && sourcePin.pin) {
      const fromPin = sourcePin.pin;
      const matchingPin = fromPin.direction === 'Output'
        ? node.inputs.find(p => arePinsCompatible(fromPin, p))
        : node.outputs.find(p => arePinsCompatible(p, fromPin));

      if (matchingPin) {
        if (fromPin.direction === 'Output') {
          this.addConnection(sourcePin.nodeId, fromPin.id, node.id, matchingPin.id);
        } else {
          this.addConnection(node.id, matchingPin.id, sourcePin.nodeId, fromPin.id);
        }
      }
    }

    this.onGraphModified();
    this.renderGraph();
    this.selectNode(node.id);
  }

  removeNode(nodeId) {
    this.currentGraph.nodes = this.currentGraph.nodes.filter(n => n.id !== nodeId);
    this.currentGraph.connections = this.currentGraph.connections.filter(c => c.fromNodeId !== nodeId && c.toNodeId !== nodeId);
    this.selectedNodeId = null;
    this.onGraphModified();
    this.renderGraph();
  }

  addConnection(fromNodeId, fromPinId, toNodeId, toPinId) {
    // If input pin is already connected, remove existing connection (single-in rule)
    this.currentGraph.connections = this.currentGraph.connections.filter(
      c => !(c.toNodeId === toNodeId && c.toPinId === toPinId)
    );

    const conn = {
      id: 'conn_' + Math.random().toString(36).substring(2, 9),
      fromNodeId,
      fromPinId,
      toNodeId,
      toPinId
    };

    this.currentGraph.connections.push(conn);
    this.onGraphModified();
    this.renderGraph();
  }

  removeConnection(connectionId) {
    this.currentGraph.connections = this.currentGraph.connections.filter(c => c.id !== connectionId);
    this.onGraphModified();
    this.renderGraph();
  }

  isPinConnected(nodeId, pinId) {
    return this.currentGraph.connections.some(
      c => (c.fromNodeId === nodeId && c.fromPinId === pinId) || (c.toNodeId === nodeId && c.toPinId === pinId)
    );
  }

  // --- VARIABLES UI ---
  updateVariablesUI() {
    if (!this.varsListEl) return;
    this.varsListEl.innerHTML = '';

    for (const v of this.currentGraph.variables) {
      const item = document.createElement('div');
      item.className = `bp-var-item ${this.selectedVariableId === v.id ? 'selected' : ''}`;

      const typePill = document.createElement('div');
      typePill.className = 'bp-var-type-pill';
      typePill.style.background = this.getPinColor(v.type);

      const nameLabel = document.createElement('span');
      nameLabel.textContent = v.name;
      nameLabel.style.flex = '1';
      nameLabel.style.marginLeft = '8px';

      const delBtn = document.createElement('button');
      delBtn.textContent = '✕';
      delBtn.style.background = 'transparent';
      delBtn.style.border = 'none';
      delBtn.style.color = '#64748b';
      delBtn.style.cursor = 'pointer';
      delBtn.style.fontSize = '10px';

      delBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        this.currentGraph.variables = this.currentGraph.variables.filter(item => item.id !== v.id);
        this.updateVariablesUI();
        this.onGraphModified();
      });

      item.appendChild(typePill);
      item.appendChild(nameLabel);
      item.appendChild(delBtn);

      item.addEventListener('click', () => {
        this.selectedVariableId = v.id;
        this.updateVariablesUI();
        this.showVariableDetails(v);
      });

      this.varsListEl.appendChild(item);
    }
  }

  getPinColor(type) {
    switch (type) {
      case 'Boolean': return '#dc2626';
      case 'Integer': return '#06b6d4';
      case 'Float': return '#22c55e';
      case 'Vector3': return '#f59e0b';
      case 'String': return '#ec4899';
      case 'ActorRef': return '#3b82f6';
      default: return '#ffffff';
    }
  }

  addVariable() {
    const name = `Var_${this.currentGraph.variables.length + 1}`;
    const v = {
      id: 'var_' + Math.random().toString(36).substring(2, 9),
      name,
      type: PinTypes.FLOAT,
      defaultValue: '0',
      isPublic: true
    };
    this.currentGraph.variables.push(v);
    this.updateVariablesUI();
    this.onGraphModified();
  }

  showVariableDetails(v) {
    const detailsContainer = document.getElementById('bp-var-details');
    if (!detailsContainer) return;

    detailsContainer.innerHTML = `
      <div style="padding:10px; font-size:11px; display:flex; flex-direction:column; gap:8px;">
        <label style="color:#94a3b8; font-weight:600;">Variable Name</label>
        <input type="text" id="bp-detail-var-name" value="${v.name}" style="background:#111317; border:1px solid #374151; color:#fff; padding:4px 6px; border-radius:3px;" />

        <label style="color:#94a3b8; font-weight:600;">Variable Type</label>
        <select id="bp-detail-var-type" style="background:#111317; border:1px solid #374151; color:#fff; padding:4px 6px; border-radius:3px;">
          <option value="Float" ${v.type === 'Float' ? 'selected' : ''}>Float</option>
          <option value="Integer" ${v.type === 'Integer' ? 'selected' : ''}>Integer</option>
          <option value="Boolean" ${v.type === 'Boolean' ? 'selected' : ''}>Boolean</option>
          <option value="Vector3" ${v.type === 'Vector3' ? 'selected' : ''}>Vector3</option>
          <option value="String" ${v.type === 'String' ? 'selected' : ''}>String</option>
        </select>

        <label style="color:#94a3b8; font-weight:600;">Default Value</label>
        <input type="text" id="bp-detail-var-val" value="${v.defaultValue}" style="background:#111317; border:1px solid #374151; color:#fff; padding:4px 6px; border-radius:3px;" />
      </div>
    `;

    const nameInput = detailsContainer.querySelector('#bp-detail-var-name');
    const typeSelect = detailsContainer.querySelector('#bp-detail-var-type');
    const valInput = detailsContainer.querySelector('#bp-detail-var-val');

    nameInput.addEventListener('change', () => {
      v.name = nameInput.value.trim().replace(/[^a-zA-Z0-9_]/g, '_');
      this.updateVariablesUI();
      this.onGraphModified();
    });

    typeSelect.addEventListener('change', () => {
      v.type = typeSelect.value;
      this.updateVariablesUI();
      this.onGraphModified();
    });

    valInput.addEventListener('input', () => {
      v.defaultValue = valInput.value;
      this.onGraphModified();
    });
  }

  // --- TAB SWITCHING ---
  switchTab(targetTab) {
    this.activeTab = targetTab;
    const viewportTabBtn = document.getElementById('tab-btn-viewport');
    const blueprintTabBtn = document.getElementById('tab-btn-blueprint');
    const viewportEl = document.getElementById('viewport');

    if (targetTab === 'blueprint') {
      if (viewportTabBtn) viewportTabBtn.classList.remove('active');
      if (blueprintTabBtn) blueprintTabBtn.classList.add('active');
      if (viewportEl) viewportEl.style.display = 'none';
      if (this.viewEl) this.viewEl.style.display = 'flex';

      requestAnimationFrame(() => {
        this.canvas.updatePinCoordinates();
        this.canvas.renderer.renderConnections(this.currentGraph.connections, this.canvas.pinCoordsMap, this.isPlaying);
      });
    } else {
      if (blueprintTabBtn) blueprintTabBtn.classList.remove('active');
      if (viewportTabBtn) viewportTabBtn.classList.add('active');
      if (this.viewEl) this.viewEl.style.display = 'none';
      if (viewportEl) viewportEl.style.display = 'block';

      // Trigger Three.js resize
      if (window.engine && typeof window.engine.onWindowResize === 'function') {
        window.engine.onWindowResize();
      }
    }
  }
}

export let blueprintManager = null;

export function initBlueprintStudio() {
  if (!blueprintManager) {
    blueprintManager = new BlueprintManager();
    window.blueprintManager = blueprintManager;
  }
  return blueprintManager;
}

window.switchCenterTab = function(tab) {
  if (window.blueprintManager) {
    window.blueprintManager.switchTab(tab);
  }
};
