/**
 * @file blueprintManager.js
 * @brief Central controller orchestrating graph state, Roslyn compilation, IPC bridge and UI.
 */

import { sendToBackend, registerHandler } from '../ipcBridge.js';
import { createNodeInstance, arePinsCompatible, PinTypes, registerDynamicNodes } from './blueprintNodes.js';
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
    this.requestNodeCatalog();
  }

  requestNodeCatalog() {
    sendToBackend('get_node_registry', {});
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
    registerHandler('get_node_registry_response', (data) => {
      if (data && data.success && data.catalog && Array.isArray(data.catalog.nodes)) {
        registerDynamicNodes(data.catalog.nodes);
        console.log(`[BlueprintManager] Ingested ${data.catalog.nodeCount || data.catalog.nodes.length} dynamic nodes via C# reflection.`);
      }
    });

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

    // --- MVC GRAPH CONTROLLER RESPONSES ---
    registerHandler('graph_create_response', (msg) => {
      const res = msg.data || msg;
      if (res && res.success && res.data) {
        this.currentGraph = this.adaptBackendDocument(res.data);
        this.isDirty = false;
        this.updateCompileBadge('clean');
        this.renderGraph();
        this.updateVariablesUI();
        this.updateCSharpPreview();
      }
    });

    registerHandler('graph_load_response', (msg) => {
      const res = msg.data || msg;
      if (res && res.success && res.data) {
        this.currentGraph = this.adaptBackendDocument(res.data);
        this.isDirty = false;
        this.updateCompileBadge('clean');
        this.renderGraph();
        this.updateVariablesUI();
        this.updateCSharpPreview();
        if (window.showToast) window.showToast(`Loaded Blueprint '${this.currentGraph.name}' via MVC Controller`);
      }
    });

    registerHandler('graph_save_response', (msg) => {
      const res = msg.data || msg;
      if (res && res.success) {
        this.isDirty = false;
        this.updateCompileBadge('clean');
        this.refreshBlueprintList();
        if (window.showToast) window.showToast(`Saved ${this.currentGraph.name}.bgraph & compiled C#`);
      } else if (res && !res.success) {
        if (window.showToast) window.showToast(`Save failed: ${res.message || 'Validation error'}`, true);
      }
    });

    registerHandler('graph_connect_pins_response', (msg) => {
      const res = msg.data || msg;
      if (res && !res.success) {
        const errMsg = res.message || (res.errors && res.errors[0]?.message) || 'Illegal connection rejected by Controller';
        if (window.showToast) window.showToast(`❌ Connection Rejected: ${errMsg}`, true);
        // Revert last pending wire
        if (this._lastPendingConnId) {
          this.currentGraph.connections = this.currentGraph.connections.filter(c => c.id !== this._lastPendingConnId);
          this._lastPendingConnId = null;
          this.renderGraph();
        }
      } else if (res && res.success && res.data) {
        const payload = res.data;
        if (payload.replacedLinkId) {
          this.currentGraph.connections = this.currentGraph.connections.filter(c => c.id !== payload.replacedLinkId);
        }
        if (this._lastPendingConnId && payload.addedLink) {
          const wire = this.currentGraph.connections.find(c => c.id === this._lastPendingConnId);
          if (wire) wire.id = payload.addedLink.id;
          this._lastPendingConnId = null;
        }
        this.renderGraph();
      }
    });

    registerHandler('graph_validate_response', (msg) => {
      const res = msg.data || msg;
      if (res) {
        if (res.isValid) {
          if (window.showToast) window.showToast('✓ Graph schema is 100% valid');
        } else {
          const errCount = (res.errors || []).length;
          if (window.showToast) window.showToast(`Graph has ${errCount} validation errors`, true);
        }
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

  adaptBackendDocument(doc) {
    if (!doc) return this.createEmptyGraph();
    const nodes = (doc.nodes || []).map(n => ({
      id: n.id,
      nodeType: n.targetMethodIdentifier,
      title: n.targetMethodIdentifier,
      category: n.nodeType === 'Event' ? 'Event' : (n.nodeType === 'PureFunction' ? 'Math' : 'Actor'),
      posX: n.x ?? n.spatial?.x ?? 0,
      posY: n.y ?? n.spatial?.y ?? 0,
      inputs: (n.pins || []).filter(p => p.direction === 'Input' || p.direction === 0).map(p => ({
        id: p.id,
        name: p.name,
        type: p.valueType,
        direction: 'Input',
        defaultValue: n.defaultPinValues?.[p.name] ?? n.defaultPinValues?.[p.id] ?? ''
      })),
      outputs: (n.pins || []).filter(p => p.direction === 'Output' || p.direction === 1).map(p => ({
        id: p.id,
        name: p.name,
        type: p.valueType,
        direction: 'Output'
      })),
      properties: { ...(n.defaultPinValues || {}) }
    }));

    const connections = (doc.links || []).map(l => ({
      id: l.id,
      fromNodeId: l.sourceNodeId,
      fromPinId: l.sourcePinId,
      toNodeId: l.targetNodeId,
      toPinId: l.targetPinId
    }));

    const variables = (doc.variables || []).map(v => ({
      id: v.id,
      name: v.name,
      type: v.type,
      defaultValue: v.defaultValue,
      isPublic: v.isPublic
    }));

    return {
      id: doc.graphId || doc.id || 'bp_' + Math.random().toString(36).substring(2, 9),
      name: doc.name || 'BP_Actor',
      type: doc.targetActorClass || 'Actor',
      nodes,
      connections,
      variables
    };
  }

  promptNewBlueprint() {
    const name = prompt('Enter new Blueprint name:', `BP_Actor_${Math.floor(Math.random() * 900 + 100)}`);
    if (!name || !name.trim()) return;

    const cleanName = name.trim().replace(/[^a-zA-Z0-9_]/g, '_');
    this.currentGraph = this.createEmptyGraph(cleanName, 'Actor');

    // Dispatch authoritative MVC creation
    sendToBackend({
      action: 'graph_action',
      subAction: 'graph_create',
      name: cleanName,
      targetActorClass: 'Actor'
    });

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
    // Dispatch authoritative MVC load
    sendToBackend({
      action: 'graph_action',
      subAction: 'graph_load',
      name
    });
    // Fallback legacy load
    sendToBackend({ action: 'load_blueprint', name });
  }

  saveBlueprint() {
    // 1. Dispatch authoritative MVC save
    sendToBackend({
      action: 'graph_action',
      subAction: 'graph_save'
    });
    // 2. Also send legacy format for backward compatibility
    sendToBackend({
      action: 'save_blueprint',
      graph: this.currentGraph
    });
  }

  compileBlueprint() {
    this.updateCompileBadge('compiling');
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

  onNodeMoved(nodeId, x, y) {
    this.onGraphModified();
    sendToBackend({
      action: 'graph_action',
      subAction: 'graph_move_node',
      nodeId,
      x,
      y
    });
  }

  onPinValueChanged(nodeId, pinId, value) {
    this.onGraphModified();
    sendToBackend({
      action: 'graph_action',
      subAction: 'graph_set_pin_value',
      nodeId,
      pinId,
      value: String(value)
    });
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

    // Dispatch authoritative MVC AddNode
    const allPins = [...node.inputs, ...node.outputs].map(p => ({
      id: p.id,
      name: p.name,
      direction: p.direction,
      type: p.type
    }));

    sendToBackend({
      action: 'graph_action',
      subAction: 'graph_add_node',
      id: node.id,
      targetMethodIdentifier: node.nodeType || nodeType,
      nodeType: node.category === 'Event' ? 'Event' : (node.category === 'Math' ? 'PureFunction' : 'ImpureFunction'),
      x: worldX,
      y: worldY,
      pins: allPins
    });

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

    // Dispatch authoritative MVC RemoveNode
    sendToBackend({
      action: 'graph_action',
      subAction: 'graph_remove_node',
      nodeId
    });
  }

  addConnection(fromNodeId, fromPinId, toNodeId, toPinId) {
    const tempId = 'conn_' + Math.random().toString(36).substring(2, 9);
    this._lastPendingConnId = tempId;

    // Optimistic single-in replacement on canvas
    this.currentGraph.connections = this.currentGraph.connections.filter(
      c => !(c.toNodeId === toNodeId && c.toPinId === toPinId)
    );

    const conn = {
      id: tempId,
      fromNodeId,
      fromPinId,
      toNodeId,
      toPinId
    };

    this.currentGraph.connections.push(conn);
    this.onGraphModified();
    this.renderGraph();

    // Dispatch authoritative MVC ConnectPins
    sendToBackend({
      action: 'graph_action',
      subAction: 'graph_connect_pins',
      sourceNodeId: fromNodeId,
      sourcePinId: fromPinId,
      targetNodeId: toNodeId,
      targetPinId: toPinId
    });
  }

  removeConnection(connectionId) {
    this.currentGraph.connections = this.currentGraph.connections.filter(c => c.id !== connectionId);
    this.onGraphModified();
    this.renderGraph();

    // Dispatch authoritative MVC DisconnectPins
    sendToBackend({
      action: 'graph_action',
      subAction: 'graph_disconnect_pins',
      linkId: connectionId
    });
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
        sendToBackend({
          action: 'graph_action',
          subAction: 'graph_remove_variable',
          variableId: v.id
        });
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
    sendToBackend({
      action: 'graph_action',
      subAction: 'graph_add_variable',
      name: v.name,
      type: v.type,
      defaultValue: v.defaultValue,
      isPublic: v.isPublic
    });
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
