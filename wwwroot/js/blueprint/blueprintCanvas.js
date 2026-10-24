/**
 * @file blueprintCanvas.js
 * @brief Infinite pannable & zoomable graph canvas, node dragging, pin connection management.
 */

import { PinTypes, PinColors, arePinsCompatible } from './blueprintNodes.js';
import { BlueprintRenderer } from './blueprintRenderer.js';
import { BlueprintContextMenu } from './blueprintContextMenu.js';

export class BlueprintCanvas {
  /**
   * @param {HTMLElement} containerEl
   * @param {Object} manager Reference to BlueprintManager
   */
  constructor(containerEl, manager) {
    this.container = containerEl;
    this.manager = manager;

    this.panX = 100;
    this.panY = 100;
    this.zoom = 1.0;
    this.minZoom = 0.25;
    this.maxZoom = 2.0;

    this.isPanning = false;
    this.panStartX = 0;
    this.panStartY = 0;

    this.draggedNode = null;
    this.nodeDragOffset = { x: 0, y: 0 };

    this.draggedPin = null; // { nodeId, pin, isInput }
    this.pinCoordsMap = new Map();

    this.initDOM();
    this.renderer = new BlueprintRenderer(this.svgLayer);
    this.contextMenu = new BlueprintContextMenu(this.container, (type, pos, props, sourcePin) => {
      this.manager.spawnNode(type, pos.x, pos.y, props, sourcePin);
    });

    this.bindEvents();
  }

  initDOM() {
    this.container.innerHTML = `
      <div class="bp-canvas-world" id="bp-canvas-world">
        <svg class="bp-svg-layer" id="bp-svg-layer"></svg>
        <div id="bp-nodes-container"></div>
      </div>
    `;

    this.worldEl = this.container.querySelector('#bp-canvas-world');
    this.svgLayer = this.container.querySelector('#bp-svg-layer');
    this.nodesContainer = this.container.querySelector('#bp-nodes-container');

    this.updateTransform();
  }

  updateTransform() {
    this.worldEl.style.transform = `translate(${this.panX}px, ${this.panY}px) scale(${this.zoom})`;
    // Sync grid background positioning
    this.container.style.backgroundPosition = `${this.panX}px ${this.panY}px`;
  }

  screenToWorld(clientX, clientY) {
    const rect = this.container.getBoundingClientRect();
    const x = (clientX - rect.left - this.panX) / this.zoom;
    const y = (clientY - rect.top - this.panY) / this.zoom;
    return { x, y };
  }

  worldToScreen(worldX, worldY) {
    const rect = this.container.getBoundingClientRect();
    return {
      x: rect.left + this.panX + (worldX * this.zoom),
      y: rect.top + this.panY + (worldY * this.zoom)
    };
  }

  /**
   * Filters available blueprint catalog nodes matching query against displayName, category, and methodName (case-insensitive).
   * @param {string} query Search filter string
   * @returns {Array<Object>} List of matching node definitions
   */
  filterActions(query = '') {
    return this.contextMenu.filterActions(query, this.manager.currentGraph?.variables || []);
  }

  /**
   * Opens the node action context menu at client screen coordinates.
   * @param {number} clientX
   * @param {number} clientY
   * @param {Object|null} [sourcePin=null]
   */
  openContextMenu(clientX, clientY, sourcePin = null) {
    const worldPos = this.screenToWorld(clientX, clientY);
    this.contextMenu.open(clientX, clientY, worldPos, this.manager.currentGraph?.variables || [], sourcePin);
  }

  /**
   * Spawns a node of specified type at given world coordinates with typed pins.
   * @param {string} nodeType
   * @param {number} worldX
   * @param {number} worldY
   * @param {Object} [props={}]
   * @param {Object|null} [sourcePin=null]
   * @returns {Object} Instantiated GraphNode
   */
  spawnNodeAt(nodeType, worldX, worldY, props = {}, sourcePin = null) {
    return this.manager.spawnNode(nodeType, worldX, worldY, props, sourcePin);
  }

  bindEvents() {
    // Pan & Zoom
    this.container.addEventListener('pointerdown', (e) => {
      // RMB (button 2) or MMB (button 1) or Space/Alt+LMB
      if (e.button === 2 || e.button === 1 || (e.button === 0 && (e.spaceKey || e.altKey))) {
        e.preventDefault();
        this.isPanning = true;
        this.panStartX = e.clientX - this.panX;
        this.panStartY = e.clientY - this.panY;
        this.container.setPointerCapture(e.pointerId);
      } else if (e.button === 0 && e.target === this.container) {
        // Deselect nodes when clicking canvas
        this.manager.selectNode(null);
      }
    });

    this.container.addEventListener('pointermove', (e) => {
      if (this.isPanning) {
        this.panX = e.clientX - this.panStartX;
        this.panY = e.clientY - this.panStartY;
        this.updateTransform();
        return;
      }

      if (this.draggedNode) {
        const worldPos = this.screenToWorld(e.clientX, e.clientY);
        this.draggedNode.posX = Math.round(worldPos.x - this.nodeDragOffset.x);
        this.draggedNode.posY = Math.round(worldPos.y - this.nodeDragOffset.y);

        const nodeEl = this.nodesContainer.querySelector(`[data-node-id="${this.draggedNode.id}"]`);
        if (nodeEl) {
          nodeEl.style.left = `${this.draggedNode.posX}px`;
          nodeEl.style.top = `${this.draggedNode.posY}px`;
        }

        this.updatePinCoordinates();
        this.renderer.renderConnections(this.manager.currentGraph.connections, this.pinCoordsMap, this.manager.isPlaying);
        return;
      }

      if (this.draggedPin) {
        const fromCoord = this.pinCoordsMap.get(`${this.draggedPin.nodeId}_${this.draggedPin.pin.id}`);
        if (fromCoord) {
          const worldPos = this.screenToWorld(e.clientX, e.clientY);
          if (this.draggedPin.pin.direction === 'Output') {
            this.renderer.drawTempWire(fromCoord.x, fromCoord.y, worldPos.x, worldPos.y, this.draggedPin.pin.type);
          } else {
            this.renderer.drawTempWire(worldPos.x, worldPos.y, fromCoord.x, fromCoord.y, this.draggedPin.pin.type);
          }
        }
      }
    });

    this.container.addEventListener('pointerup', (e) => {
      if (this.isPanning) {
        this.isPanning = false;
        try { this.container.releasePointerCapture(e.pointerId); } catch {}
      }

      if (this.draggedNode) {
        const movedNode = this.draggedNode;
        this.draggedNode = null;
        this.manager.onNodeMoved(movedNode.id, movedNode.posX, movedNode.posY);
      }

      if (this.draggedPin) {
        // Check if dropped on empty canvas -> open filtered context menu
        const targetPinEl = document.elementFromPoint(e.clientX, e.clientY)?.closest('.bp-pin-icon');
        if (!targetPinEl) {
          const worldPos = this.screenToWorld(e.clientX, e.clientY);
          this.contextMenu.open(e.clientX, e.clientY, worldPos, this.manager.currentGraph.variables, this.draggedPin);
        }
        this.draggedPin = null;
        this.renderer.clearTempWire();
      }
    });

    // Zoom & Pan with Touchpad / Wheel
    this.container.addEventListener('wheel', (e) => {
      e.preventDefault();
      const rect = this.container.getBoundingClientRect();
      const mouseX = e.clientX - rect.left;
      const mouseY = e.clientY - rect.top;

      if (e.ctrlKey) {
        // Pinch-to-zoom on touchpad (or Ctrl + Wheel on mouse)
        const worldBefore = {
          x: (mouseX - this.panX) / this.zoom,
          y: (mouseY - this.panY) / this.zoom
        };

        const zoomFactor = Math.pow(0.995, e.deltaY);
        const newZoom = Math.min(Math.max(this.zoom * zoomFactor, this.minZoom), this.maxZoom);

        this.zoom = newZoom;
        this.panX = mouseX - (worldBefore.x * this.zoom);
        this.panY = mouseY - (worldBefore.y * this.zoom);
        this.updateTransform();
      } else if (e.deltaMode === 0 && (Math.abs(e.deltaX) > 0 || Math.abs(e.deltaY) < 40 || e.shiftKey)) {
        // Two-finger trackpad 2D pan
        this.panX -= e.deltaX;
        this.panY -= e.deltaY;
        this.updateTransform();
      } else {
        // Traditional discrete mouse wheel zoom
        const worldBefore = {
          x: (mouseX - this.panX) / this.zoom,
          y: (mouseY - this.panY) / this.zoom
        };

        const zoomFactor = e.deltaY < 0 ? 1.12 : 0.89;
        const newZoom = Math.min(Math.max(this.zoom * zoomFactor, this.minZoom), this.maxZoom);

        this.zoom = newZoom;
        this.panX = mouseX - (worldBefore.x * this.zoom);
        this.panY = mouseY - (worldBefore.y * this.zoom);
        this.updateTransform();
      }
    }, { passive: false });

    // Right-click context menu
    this.container.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      if (!this.draggedNode && !this.draggedPin) {
        this.openContextMenu(e.clientX, e.clientY);
      }
    });

    // Delete keyboard shortcut
    window.addEventListener('keydown', (e) => {
      if (document.activeElement && (document.activeElement.tagName === 'INPUT' || document.activeElement.tagName === 'TEXTAREA')) {
        return;
      }
      if (e.key === 'Delete' || e.key === 'Backspace') {
        if (this.renderer.selectedConnectionId) {
          this.manager.removeConnection(this.renderer.selectedConnectionId);
          this.renderer.selectedConnectionId = null;
        } else if (this.manager.selectedNodeId) {
          this.manager.removeNode(this.manager.selectedNodeId);
        }
      }
    });
  }

  renderGraph(graph) {
    this.nodesContainer.innerHTML = '';

    for (const node of graph.nodes) {
      const nodeEl = this.createNodeElement(node);
      this.nodesContainer.appendChild(nodeEl);
    }

    requestAnimationFrame(() => {
      this.updatePinCoordinates();
      this.renderer.renderConnections(graph.connections, this.pinCoordsMap, this.manager.isPlaying);
    });
  }

  createNodeElement(node) {
    const el = document.createElement('div');
    el.className = `bp-node ${this.manager.selectedNodeId === node.id ? 'selected' : ''}`;
    el.dataset.nodeId = node.id;
    el.style.left = `${node.posX}px`;
    el.style.top = `${node.posY}px`;

    const catClass = (node.category || 'actor').toLowerCase().replace(/\s+/g, '');

    // Header
    const headerEl = document.createElement('div');
    headerEl.className = `bp-node-header ${catClass}`;
    headerEl.innerHTML = `
      <svg viewBox="0 0 16 16" width="12" height="12" fill="currentColor">
        <path d="M8 1L2 5v6l6 4 6-4V5L8 1z"/>
      </svg>
      <span>${node.title}</span>
    `;

    headerEl.addEventListener('pointerdown', (e) => {
      if (e.button !== 0) return;
      e.stopPropagation();
      this.manager.selectNode(node.id);
      this.draggedNode = node;
      const worldPos = this.screenToWorld(e.clientX, e.clientY);
      this.nodeDragOffset = {
        x: worldPos.x - node.posX,
        y: worldPos.y - node.posY
      };
    });

    el.appendChild(headerEl);

    // Body (Pins)
    const bodyEl = document.createElement('div');
    bodyEl.className = 'bp-node-body';

    const leftCol = document.createElement('div');
    leftCol.className = 'bp-pins-column bp-pins-left';

    const rightCol = document.createElement('div');
    rightCol.className = 'bp-pins-column bp-pins-right';

    // Inputs
    for (const pin of node.inputs) {
      const isConnected = this.manager.isPinConnected(node.id, pin.id);
      const row = document.createElement('div');
      row.className = 'bp-pin-row';
      row.dataset.pinId = pin.id;

      const pinIcon = this.createPinIcon(pin, isConnected);
      this.attachPinEvents(pinIcon, node, pin);

      row.appendChild(pinIcon);

      const nameSpan = document.createElement('span');
      nameSpan.className = 'bp-pin-name';
      nameSpan.textContent = pin.name;
      row.appendChild(nameSpan);

      // Inline editable input if unplugged and data pin
      if (!isConnected && pin.type !== PinTypes.EXEC && pin.type !== PinTypes.ACTOR_REF) {
        const inputEl = this.createInlineInput(node, pin);
        row.appendChild(inputEl);
      }

      leftCol.appendChild(row);
    }

    // Outputs
    for (const pin of node.outputs) {
      const isConnected = this.manager.isPinConnected(node.id, pin.id);
      const row = document.createElement('div');
      row.className = 'bp-pin-row';
      row.dataset.pinId = pin.id;

      const nameSpan = document.createElement('span');
      nameSpan.className = 'bp-pin-name';
      nameSpan.textContent = pin.name;
      row.appendChild(nameSpan);

      const pinIcon = this.createPinIcon(pin, isConnected);
      this.attachPinEvents(pinIcon, node, pin);

      row.appendChild(pinIcon);
      rightCol.appendChild(row);
    }

    bodyEl.appendChild(leftCol);
    bodyEl.appendChild(rightCol);
    el.appendChild(bodyEl);

    return el;
  }

  createPinIcon(pin, isConnected) {
    const color = PinColors[pin.type] || '#ffffff';
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('class', 'bp-pin-icon');
    svg.setAttribute('viewBox', '0 0 16 16');

    if (pin.type === PinTypes.EXEC) {
      // Pentagon arrow shape for Exec
      svg.innerHTML = `<path d="M3 2l8 6-8 6V2z" fill="${isConnected ? color : 'none'}" stroke="${color}" stroke-width="2"/>`;
    } else {
      // Circle for data pins
      svg.innerHTML = `<circle cx="8" cy="8" r="5" fill="${isConnected ? color : '#111317'}" stroke="${color}" stroke-width="2"/>`;
    }

    return svg;
  }

  createInlineInput(node, pin) {
    const input = document.createElement('input');
    input.className = 'bp-inline-input';

    const currentVal = node.properties[pin.name] ?? pin.defaultValue ?? '';
    input.value = currentVal;

    if (pin.type === PinTypes.BOOLEAN) {
      input.type = 'checkbox';
      input.checked = currentVal === 'true' || currentVal === true;
      input.style.width = '16px';
      input.addEventListener('change', () => {
        const val = input.checked ? 'true' : 'false';
        node.properties[pin.name] = val;
        this.manager.onPinValueChanged(node.id, pin.id, val);
      });
    } else {
      input.type = pin.type === PinTypes.INTEGER || pin.type === PinTypes.FLOAT ? 'number' : 'text';
      if (pin.type === PinTypes.FLOAT) input.step = '0.1';
      input.addEventListener('change', () => {
        node.properties[pin.name] = input.value;
        this.manager.onPinValueChanged(node.id, pin.id, input.value);
      });
      input.addEventListener('input', () => {
        node.properties[pin.name] = input.value;
        this.manager.onGraphModified();
      });
    }

    input.addEventListener('pointerdown', (e) => e.stopPropagation());
    return input;
  }

  attachPinEvents(pinIcon, node, pin) {
    pinIcon.addEventListener('pointerdown', (e) => {
      e.stopPropagation();
      this.draggedPin = { nodeId: node.id, pin };
    });

    pinIcon.addEventListener('pointerup', (e) => {
      e.stopPropagation();
      if (!this.draggedPin) return;

      const fromPin = this.draggedPin.pin;
      const toPin = pin;

      if (this.draggedPin.nodeId !== node.id && arePinsCompatible(fromPin, toPin)) {
        if (fromPin.direction === 'Output' && toPin.direction === 'Input') {
          this.manager.addConnection(this.draggedPin.nodeId, fromPin.id, node.id, toPin.id);
        } else if (fromPin.direction === 'Input' && toPin.direction === 'Output') {
          this.manager.addConnection(node.id, toPin.id, this.draggedPin.nodeId, fromPin.id);
        }
      }

      this.draggedPin = null;
      this.renderer.clearTempWire();
    });
  }

  updatePinCoordinates() {
    this.pinCoordsMap.clear();

    const pinIcons = this.nodesContainer.querySelectorAll('.bp-pin-icon');
    for (const icon of pinIcons) {
      const row = icon.closest('.bp-pin-row');
      const nodeEl = icon.closest('.bp-node');
      if (!row || !nodeEl) continue;

      const nodeId = nodeEl.dataset.nodeId;
      const pinId = row.dataset.pinId;

      const rect = icon.getBoundingClientRect();
      const worldPos = this.screenToWorld(rect.left + (rect.width / 2), rect.top + (rect.height / 2));

      const node = this.manager.currentGraph.nodes.find(n => n.id === nodeId);
      const pin = node ? [...node.inputs, ...node.outputs].find(p => p.id === pinId) : null;

      if (pin) {
        this.pinCoordsMap.set(`${nodeId}_${pinId}`, {
          x: worldPos.x,
          y: worldPos.y,
          pin
        });
      }
    }
  }
}
