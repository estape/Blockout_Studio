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

  populateList(listEl, filter, variables) {
    listEl.innerHTML = '';

    const categories = {
      'Events': [],
      'Flow Control': [],
      'Actor Operations': [],
      'Math & Logic': []
    };
    const seenItems = new Set();

    // Populate registry nodes
    for (const [key, def] of Object.entries(NodeRegistry)) {
      if (key === 'GetVariable' || key === 'SetVariable') continue;

      if (filter && !def.title.toLowerCase().includes(filter) && !key.toLowerCase().includes(filter)) {
        continue;
      }

      // Avoid duplicating aliases with same title and category
      const dedupeKey = `${def.category || 'General'}::${def.title}`;
      if (seenItems.has(dedupeKey)) continue;
      seenItems.add(dedupeKey);

      const rawCat = def.category || 'General';
      const catName = rawCat === 'Event' ? 'Events'
        : rawCat === 'FlowControl' ? 'Flow Control'
        : rawCat === 'Actor' ? 'Actor Operations'
        : rawCat === 'Math' ? 'Math & Logic'
        : rawCat;

      if (!categories[catName]) {
        categories[catName] = [];
      }

      categories[catName].push({
        type: key,
        title: def.title,
        props: {}
      });
    }

    // Populate Variables
    for (const v of variables) {
      if (!filter || `get ${v.name}`.toLowerCase().includes(filter)) {
        categories['Variables'].push({
          type: 'GetVariable',
          title: `Get ${v.name}`,
          props: { VariableName: v.name }
        });
      }
      if (!filter || `set ${v.name}`.toLowerCase().includes(filter)) {
        categories['Variables'].push({
          type: 'SetVariable',
          title: `Set ${v.name}`,
          props: { VariableName: v.name }
        });
      }
    }

    let hasAny = false;
    for (const [cat, items] of Object.entries(categories)) {
      if (items.length === 0) continue;
      hasAny = true;

      const catHeader = document.createElement('div');
      catHeader.className = 'bp-context-cat';
      catHeader.textContent = cat;
      listEl.appendChild(catHeader);

      for (const item of items) {
        const itemEl = document.createElement('div');
        itemEl.className = 'bp-context-item';
        itemEl.tabIndex = 0;
        itemEl.innerHTML = `<span>${item.title}</span>`;

        itemEl.addEventListener('click', () => {
          this.onSelectNode(item.type, this.targetWorldPos, item.props, this.draggedSourcePin);
          this.close();
        });

        itemEl.addEventListener('keydown', (e) => {
          if (e.key === 'Enter') {
            this.onSelectNode(item.type, this.targetWorldPos, item.props, this.draggedSourcePin);
            this.close();
          }
        });

        listEl.appendChild(itemEl);
      }
    }

    if (!hasAny) {
      const emptyEl = document.createElement('div');
      emptyEl.style.padding = '12px';
      emptyEl.style.color = '#64748b';
      emptyEl.style.textAlign = 'center';
      emptyEl.textContent = 'No matching actions found.';
      listEl.appendChild(emptyEl);
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
