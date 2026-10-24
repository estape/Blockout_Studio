/**
 * @file blueprintContextMenu.js
 * @brief Searchable action context menu ("All Actions for this Blueprint") for node spawning.
 */

import { NodeRegistry } from './blueprintNodes.js';

export class BlueprintContextMenu {
  constructor(containerEl, onSelectNode) {
    this.container = containerEl;
    this.onSelectNode = onSelectNode;
    this.menuEl = null;
    this.targetWorldPos = { x: 0, y: 0 };
    this.draggedSourcePin = null;
  }

  /**
   * Opens the context menu at specified client screen coordinates.
   * @param {number} clientX
   * @param {number} clientY
   * @param {{x: number, y: number}} worldPos
   * @param {Array<Object>} currentVariables
   * @param {Object|null} [sourcePin=null]
   */
  open(clientX, clientY, worldPos, currentVariables = [], sourcePin = null) {
    this.close();
    this.targetWorldPos = worldPos;
    this.draggedSourcePin = sourcePin;

    this.menuEl = document.createElement('div');
    this.menuEl.className = 'bp-context-menu';

    // Position within viewport boundaries
    const maxX = window.innerWidth - 270;
    const maxY = window.innerHeight - 390;
    const posX = Math.min(clientX, maxX);
    const posY = Math.min(clientY, maxY);

    this.menuEl.style.left = `${posX}px`;
    this.menuEl.style.top = `${posY}px`;

    this.menuEl.innerHTML = `
      <div class="bp-context-search">
        <input type="text" id="bp-ctx-search-input" placeholder="Search actions... (e.g. Tick, Branch, Set Location)" />
      </div>
      <div class="bp-context-list" id="bp-ctx-list"></div>
    `;

    document.body.appendChild(this.menuEl);

    const searchInput = this.menuEl.querySelector('#bp-ctx-search-input');
    const listEl = this.menuEl.querySelector('#bp-ctx-list');

    this.populateList(listEl, '', currentVariables);

    searchInput.focus();

    searchInput.addEventListener('input', (e) => {
      this.populateList(listEl, e.target.value.trim().toLowerCase(), currentVariables);
    });

    searchInput.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') {
        this.close();
      } else if (e.key === 'ArrowDown') {
        e.preventDefault();
        const first = listEl.querySelector('.bp-context-item');
        if (first) first.focus();
      }
    });

    // Dismiss on click outside
    const dismissHandler = (e) => {
      if (this.menuEl && !this.menuEl.contains(e.target)) {
        this.close();
        document.removeEventListener('pointerdown', dismissHandler);
      }
    };
    setTimeout(() => document.addEventListener('pointerdown', dismissHandler), 10);
  }

  /**
   * Filters available blueprint catalog nodes matching query against displayName, category, and methodName (case-insensitive).
   * @param {string} filter Search query string
   * @param {Array<Object>} variables Current graph variables
   * @returns {Array<Object>} List of matching action objects
   */
  filterActions(filter = '', variables = []) {
    const q = (filter || '').trim().toLowerCase();
    const results = [];
    const seen = new Set();

    // 1. Inspect window.nodeCatalog if available (dynamically ingested from C#)
    if (typeof window !== 'undefined' && Array.isArray(window.nodeCatalog)) {
      for (const node of window.nodeCatalog) {
        if (!node || !node.id) continue;
        const displayName = String(node.title || node.nodeType || node.id).trim();
        const category = String(node.category || 'General').trim();
        const methodName = String(node.methodName || node.nodeType || '').trim();

        if (q) {
          const qNoSpace = q.replace(/\s+/g, '');
          const catNoSpace = category.replace(/\s+/g, '').toLowerCase();
          const matchDisplay = displayName.toLowerCase().includes(q);
          const matchCategory = category.toLowerCase().includes(q) || catNoSpace.includes(qNoSpace);
          const matchMethod = methodName.toLowerCase().includes(q);
          const matchId = node.id.toLowerCase().includes(q);
          if (!matchDisplay && !matchCategory && !matchMethod && !matchId) continue;
        }

        const dedupeKey = `${category}::${displayName}`;
        if (!seen.has(dedupeKey)) {
          seen.add(dedupeKey);
          results.push({
            type: node.id,
            title: displayName,
            displayName,
            category,
            methodName,
            props: node.properties || {}
          });
        }
      }
    }

    // 2. Inspect NodeRegistry (built-in and statically registered nodes)
    for (const [key, def] of Object.entries(NodeRegistry)) {
      if (key === 'GetVariable' || key === 'SetVariable') continue;

      const displayName = String(def.displayName || def.title || key).trim();
      const category = String(def.category || 'General').trim();
      const methodName = String(def.methodName || def.type || key).trim();

      if (q) {
        const qNoSpace = q.replace(/\s+/g, '');
        const catNoSpace = category.replace(/\s+/g, '').toLowerCase();
        const matchDisplay = displayName.toLowerCase().includes(q);
        const matchCategory = category.toLowerCase().includes(q) || catNoSpace.includes(qNoSpace);
        const matchMethod = methodName.toLowerCase().includes(q);
        const matchKey = key.toLowerCase().includes(q);
        if (!matchDisplay && !matchCategory && !matchMethod && !matchKey) continue;
      }

      const dedupeKey = `${category}::${displayName}`;
      if (!seen.has(dedupeKey)) {
        seen.add(dedupeKey);
        results.push({
          type: key,
          title: displayName,
          displayName,
          category,
          methodName,
          props: def.properties || {}
        });
      }
    }

    // 3. Inspect Variables
    for (const v of (variables || [])) {
      if (!v || !v.name) continue;
      if (!q || `get ${v.name}`.toLowerCase().includes(q) || 'variables'.includes(q) || 'get'.includes(q)) {
        results.push({
          type: 'GetVariable',
          title: `Get ${v.name}`,
          displayName: `Get ${v.name}`,
          category: 'Variables',
          methodName: `Get${v.name}`,
          props: { VariableName: v.name }
        });
      }
      if (!q || `set ${v.name}`.toLowerCase().includes(q) || 'variables'.includes(q) || 'set'.includes(q)) {
        results.push({
          type: 'SetVariable',
          title: `Set ${v.name}`,
          displayName: `Set ${v.name}`,
          category: 'Variables',
          methodName: `Set${v.name}`,
          props: { VariableName: v.name }
        });
      }
    }

    return results;
  }

  populateList(listEl, filter, variables) {
    listEl.innerHTML = '';

    const items = this.filterActions(filter, variables);
    if (!items || items.length === 0) {
      const emptyEl = document.createElement('div');
      emptyEl.style.padding = '12px';
      emptyEl.style.color = '#64748b';
      emptyEl.style.textAlign = 'center';
      emptyEl.textContent = 'No matching actions found.';
      listEl.appendChild(emptyEl);
      return;
    }

    // Group items by category
    const categories = {
      'Events': [],
      'Flow Control': [],
      'Actor Operations': [],
      'Math & Logic': [],
      'Variables': []
    };

    for (const item of items) {
      const rawCat = item.category || 'General';
      const catName = rawCat === 'Event' ? 'Events'
        : (rawCat === 'FlowControl' || rawCat === 'Flow Control') ? 'Flow Control'
        : rawCat === 'Actor' ? 'Actor Operations'
        : (rawCat === 'Math' || rawCat === 'Math & Logic') ? 'Math & Logic'
        : rawCat;

      if (!categories[catName]) {
        categories[catName] = [];
      }
      categories[catName].push(item);
    }

    for (const [cat, catItems] of Object.entries(categories)) {
      if (!catItems || catItems.length === 0) continue;

      const catHeader = document.createElement('div');
      catHeader.className = 'bp-context-cat';
      catHeader.textContent = cat;
      listEl.appendChild(catHeader);

      for (const item of catItems) {
        const itemEl = document.createElement('div');
        itemEl.className = 'bp-context-item';
        itemEl.tabIndex = 0;
        itemEl.setAttribute('role', 'button');
        itemEl.dataset.nodeType = item.type;
        itemEl.innerHTML = `<span>${item.title || item.displayName}</span>`;

        const chooseItem = (e) => {
          if (e) {
            e.preventDefault();
            e.stopPropagation();
          }
          this.onSelectNode(item.type, this.targetWorldPos, item.props, this.draggedSourcePin);
          this.close();
        };

        itemEl.addEventListener('click', chooseItem);
        itemEl.addEventListener('keydown', (e) => {
          if (e.key === 'Enter') {
            chooseItem(e);
          }
        });

        listEl.appendChild(itemEl);
      }
    }
  }

  close() {
    if (this.menuEl && this.menuEl.parentNode) {
      this.menuEl.parentNode.removeChild(this.menuEl);
    }
    this.menuEl = null;
    this.draggedSourcePin = null;
  }
}
