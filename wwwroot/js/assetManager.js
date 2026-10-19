// ============================================================================
// CONTENT BROWSER & HYBRID ASSET MANAGER (.meta Sidecars + Fast Registry Cache)
// ============================================================================

import { sendToBackend, registerHandler, showToast } from './ipcBridge.js';
import { createBlock, selectBlock, blocks } from './sceneManager.js';
import { camera, scene, raycaster } from './engine.js';
import { requestMeshBinary } from './usdObjLoader.js';

let isOpen = false;
let isDocked = false;
let activeFolder = '/Game';
let activeCollection = null;
let activeTypeFilter = 'all';
let searchQuery = '';
let currentRegistry = { assets: [] };
let selectedAssetGuid = null;
let contextTargetItem = null;

// Color mapping for asset types
export const ASSET_TYPE_COLORS = {
  StaticMesh: '#00e5ff',   // Cyan
  UsdStage: '#f58220',     // Orange
  Material: '#2ecc71',     // Green
  Shader: '#a855f7',       // Purple
  Texture: '#ef4444',       // Red
  Scene: '#eab308',         // Yellow
  Audio: '#ec4899',         // Pink
  Script: '#3b82f6',        // Blue
  Prefab: '#06b6d4',        // Teal
  Unknown: '#64748b'        // Slate Gray
};

export const ASSET_TYPE_ICONS = {
  StaticMesh: '🧊',
  UsdStage: '🌐',
  Material: '🎨',
  Shader: '⚡',
  Texture: '🖼️',
  Scene: '🎬',
  Audio: '🔊',
  Script: '📜',
  Prefab: '📦',
  Unknown: '📄'
};

// ============================================================================
// INITIALIZATION & IPC LISTENERS
// ============================================================================

export function initAssetManager() {
  // Query initial project status and asset registry
  sendToBackend('get_project_status');
  sendToBackend('get_asset_registry');

  // Register IPC responses
  registerHandler('project_status_response', (msg) => {
    if (msg.success && msg.projectName) {
      updateProjectTitle(msg.projectName, msg.projectRoot);
    }
  });

  registerHandler('asset_registry_response', (msg) => {
    if (msg.registry) {
      currentRegistry = msg.registry;
      renderFolderTree();
      renderFilterPills();
      renderAssetGrid();
    }
  });

  registerHandler('asset_database_updated', (msg) => {
    if (msg.registry) {
      currentRegistry = msg.registry;
      renderFolderTree();
      renderFilterPills();
      renderAssetGrid();
      showToast('Asset Database synchronized in real-time');
    }
  });

  registerHandler('import_asset_response', (msg) => {
    if (msg.success) {
      showToast('Imported asset: ' + (msg.item?.fileName || 'Success'));
      if (msg.registry) currentRegistry = msg.registry;
      renderFolderTree();
      renderAssetGrid();
    } else if (msg.message) {
      showToast(msg.message, true);
    }
  });

  registerHandler('reimport_asset_response', (msg) => {
    showToast(msg.success ? 'Asset reimported successfully' : 'Reimport failed', !msg.success);
  });

  registerHandler('delete_asset_response', (msg) => {
    showToast(msg.success ? 'Asset deleted' : 'Delete failed', !msg.success);
  });

  registerHandler('asset_content_response', (msg) => {
    if (msg.success && msg.content) {
      handleSpawnAssetContent(msg);
    }
  });

  // Setup DOM Event Listeners
  setupDrawerEventListeners();
  setupViewportDropTarget();
}

function updateProjectTitle(name, root) {
  const brandEl = document.querySelector('.brand span:nth-child(3)');
  if (brandEl) {
    brandEl.textContent = `Blockout Studio — [${name}]`;
  }
}

// ============================================================================
// CONTENT DRAWER TOGGLE & DOCKING (Ctrl + Space)
// ============================================================================

export function toggleContentDrawer() {
  isOpen = !isOpen;
  const drawer = document.getElementById('content-drawer');
  const pill = document.getElementById('drawer-pill');

  if (drawer) {
    drawer.classList.toggle('open', isOpen);
  }
  if (pill) {
    pill.classList.toggle('active', isOpen);
  }

  if (isOpen) {
    sendToBackend('get_asset_registry');
    const searchInput = document.getElementById('cb-search');
    if (searchInput) searchInput.focus();
  }
}

export function togglePinDrawer() {
  isDocked = !isDocked;
  const drawer = document.getElementById('content-drawer');
  const workspace = document.getElementById('workspace');
  const pinBtn = document.getElementById('btn-pin-drawer');

  if (drawer) {
    drawer.classList.toggle('docked', isDocked);
  }
  if (workspace) {
    workspace.classList.toggle('drawer-docked', isDocked);
  }
  if (pinBtn) {
    pinBtn.textContent = isDocked ? '📌 Pinned' : '📍 Dock';
  }

  // Notify window resize for Three.js viewport
  window.dispatchEvent(new Event('resize'));
}

function setupDrawerEventListeners() {
  // Search input
  const searchInput = document.getElementById('cb-search');
  if (searchInput) {
    searchInput.addEventListener('input', (e) => {
      searchQuery = e.target.value.trim();
      renderAssetGrid();
    });
  }

  // Close context menu on outside click
  window.addEventListener('click', () => {
    hideContextMenu();
  });
}

// ============================================================================
// FOLDER TREE & COLLECTIONS (Left Pane)
// ============================================================================

function renderFolderTree() {
  const treeContainer = document.getElementById('cb-folder-tree');
  if (!treeContainer) return;

  // Extract unique folders under /Game
  const folders = new Set(['/Game']);
  (currentRegistry.assets || []).forEach((asset) => {
    const parts = (asset.virtualPath || '').split('/').filter(Boolean);
    if (parts.length > 0) {
      let acc = '/' + parts[0];
      folders.add(acc);
      for (let i = 1; i < parts.length - 1; i++) {
        acc += '/' + parts[i];
        folders.add(acc);
      }
    }
  });

  // Ensure standard folders exist in list
  ['/Game/Meshes', '/Game/Materials', '/Game/Shaders', '/Game/Textures', '/Game/Scenes', '/Game/Scripts', '/Game/Audio', '/Game/Prefabs'].forEach(f => folders.add(f));

  const sortedFolders = Array.from(folders).sort();

  let html = '';
  sortedFolders.forEach((folder) => {
    const depth = (folder.match(/\//g) || []).length - 1;
    const name = folder === '/Game' ? 'Content (/Game)' : folder.split('/').pop();
    const isSelected = activeFolder === folder && activeCollection === null;

    html += `
      <div class="tree-node ${isSelected ? 'active' : ''}" style="padding-left: ${depth * 14 + 8}px;" onclick="selectFolder('${folder}')">
        <span class="tree-icon">📁</span>
        <span class="tree-label">${name}</span>
      </div>
    `;
  });

  treeContainer.innerHTML = html;
}

export function selectFolder(folder) {
  activeFolder = folder;
  activeCollection = null;
  renderFolderTree();
  updateBreadcrumbs();
  renderAssetGrid();
}

export function selectCollection(colKey) {
  activeCollection = colKey;
  activeFolder = '/Game';
  renderFolderTree();
  updateBreadcrumbs();
  renderAssetGrid();
}

function updateBreadcrumbs() {
  const bcContainer = document.getElementById('cb-breadcrumbs');
  if (!bcContainer) return;

  if (activeCollection) {
    bcContainer.innerHTML = `<span onclick="selectFolder('/Game')">Content</span> &gt; <span class="bc-active">Collection: ${activeCollection}</span>`;
    return;
  }

  const parts = activeFolder.split('/').filter(Boolean);
  let acc = '';
  let html = '';

  parts.forEach((p, idx) => {
    acc += '/' + p;
    const isLast = idx === parts.length - 1;
    const label = p === 'Game' ? 'Content' : p;
    if (isLast) {
      html += `<span class="bc-active">${label}</span>`;
    } else {
      const clickPath = acc;
      html += `<span onclick="selectFolder('${clickPath}')">${label}</span> &gt; `;
    }
  });

  bcContainer.innerHTML = html || '<span>Content</span>';
}

// ============================================================================
// FILTER PILLS & TOKENIZED SEARCH
// ============================================================================

function renderFilterPills() {
  const container = document.getElementById('cb-filter-pills');
  if (!container) return;

  const assets = currentRegistry.assets || [];
  const counts = {
    all: assets.length,
    StaticMesh: assets.filter(a => a.assetType === 'StaticMesh').length,
    UsdStage: assets.filter(a => a.assetType === 'UsdStage').length,
    Material: assets.filter(a => a.assetType === 'Material').length,
    Shader: assets.filter(a => a.assetType === 'Shader').length,
    Texture: assets.filter(a => a.assetType === 'Texture').length,
    Scene: assets.filter(a => a.assetType === 'Scene').length
  };

  const pills = [
    { key: 'all', label: 'All', color: '#94a3b8', count: counts.all },
    { key: 'StaticMesh', label: 'Static Mesh', color: ASSET_TYPE_COLORS.StaticMesh, count: counts.StaticMesh },
    { key: 'UsdStage', label: 'USD Stage', color: ASSET_TYPE_COLORS.UsdStage, count: counts.UsdStage },
    { key: 'Material', label: 'Material', color: ASSET_TYPE_COLORS.Material, count: counts.Material },
    { key: 'Shader', label: 'Shader', color: ASSET_TYPE_COLORS.Shader, count: counts.Shader },
    { key: 'Texture', label: 'Texture', color: ASSET_TYPE_COLORS.Texture, count: counts.Texture },
    { key: 'Scene', label: 'Scene', color: ASSET_TYPE_COLORS.Scene, count: counts.Scene }
  ];

  let html = '';
  pills.forEach((p) => {
    const active = activeTypeFilter === p.key;
    html += `
      <button class="filter-pill ${active ? 'active' : ''}" style="--pill-color: ${p.color};" onclick="setAssetTypeFilter('${p.key}')">
        <span>${p.label}</span>
        <span class="pill-count">${p.count}</span>
      </button>
    `;
  });

  container.innerHTML = html;
}

export function setAssetTypeFilter(filterKey) {
  activeTypeFilter = filterKey;
  renderFilterPills();
  renderAssetGrid();
}

// ============================================================================
// MAIN ASSET GRID & CARDS
// ============================================================================

function renderAssetGrid() {
  const grid = document.getElementById('cb-asset-grid');
  if (!grid) return;

  let items = currentRegistry.assets || [];

  // 1. Folder or Collection Filter
  if (activeCollection) {
    if (activeCollection === 'meshes') items = items.filter(a => a.assetType === 'StaticMesh');
    else if (activeCollection === 'usd') items = items.filter(a => a.assetType === 'UsdStage');
    else if (activeCollection === 'materials') items = items.filter(a => a.assetType === 'Material');
    else if (activeCollection === 'shaders') items = items.filter(a => a.assetType === 'Shader');
    else if (activeCollection === 'textures') items = items.filter(a => a.assetType === 'Texture');
    else if (activeCollection === 'scenes') items = items.filter(a => a.assetType === 'Scene');
  } else if (activeFolder) {
    items = items.filter(a => {
      if (activeFolder === '/Game') return true;
      return a.virtualPath.startsWith(activeFolder);
    });
  }

  // 2. Type Filter Pill
  if (activeTypeFilter !== 'all') {
    items = items.filter(a => a.assetType === activeTypeFilter);
  }

  // 3. Search Tokenizer: parses t:Mesh, t:USD, or free text
  if (searchQuery) {
    const q = searchQuery.toLowerCase();
    if (q.startsWith('t:')) {
      const typeSub = q.substring(2);
      items = items.filter(a => a.assetType.toLowerCase().includes(typeSub));
    } else {
      items = items.filter(a =>
        a.fileName.toLowerCase().includes(q) ||
        a.virtualPath.toLowerCase().includes(q) ||
        a.guid.toLowerCase().includes(q)
      );
    }
  }

  if (items.length === 0) {
    grid.innerHTML = `
      <div style="grid-column: 1 / -1; padding: 40px; text-align: center; color: #64748b; font-size: 13px;">
        No assets found in this folder matching filters.<br/>
        <span style="font-size: 11px; color: #475569;">Click "Import" or drag files from Windows Explorer into this drawer.</span>
      </div>
    `;
    return;
  }

  let html = '';
  items.forEach((item) => {
    const color = ASSET_TYPE_COLORS[item.assetType] || ASSET_TYPE_COLORS.Unknown;
    const icon = ASSET_TYPE_ICONS[item.assetType] || '📄';
    const isSelected = selectedAssetGuid === item.guid;
    const sizeKb = (item.fileSize / 1024).toFixed(1);

    html += `
      <div class="asset-tile ${isSelected ? 'selected' : ''}"
           style="border-bottom-color: ${color};"
           draggable="true"
           data-guid="${item.guid}"
           data-path="${item.virtualPath}"
           data-type="${item.assetType}"
           data-name="${item.fileName}"
           onclick="selectAssetTile('${item.guid}')"
           ondblclick="handleAssetDoubleClick('${item.guid}')"
           oncontextmenu="handleAssetContextMenu(event, '${item.guid}')">
        <div class="asset-tile-thumb" style="color: ${color};">
          <span style="font-size: 26px;">${icon}</span>
        </div>
        <div class="asset-tile-info">
          <div class="asset-tile-name" title="${item.fileName}">${item.fileName}</div>
          <div class="asset-tile-meta">${item.assetType} &bull; ${sizeKb} KB</div>
        </div>
      </div>
    `;
  });

  grid.innerHTML = html;

  // Attach Dragstart events
  grid.querySelectorAll('.asset-tile').forEach((tile) => {
    tile.addEventListener('dragstart', (e) => {
      const guid = tile.getAttribute('data-guid');
      const item = currentRegistry.assets.find(a => a.guid === guid);
      if (item) {
        e.dataTransfer.setData('application/json', JSON.stringify(item));
        e.dataTransfer.effectAllowed = 'copyMove';
      }
    });
  });
}

export function selectAssetTile(guid) {
  selectedAssetGuid = guid;
  const grid = document.getElementById('cb-asset-grid');
  if (grid) {
    grid.querySelectorAll('.asset-tile').forEach(t => {
      t.classList.toggle('selected', t.getAttribute('data-guid') === guid);
    });
  }
}

export function handleAssetDoubleClick(guid) {
  const item = (currentRegistry.assets || []).find(a => a.guid === guid);
  if (!item) return;

  if (item.assetType === 'Scene') {
    if (confirm(`Open scene '${item.fileName}'? Any unsaved changes in current level will be replaced.`)) {
      sendToBackend('get_asset_content', { guid: item.guid });
    }
  } else if (item.assetType === 'StaticMesh' || item.assetType === 'UsdStage') {
    requestMeshBinary(item.guid, new THREE.Vector3(0, 0, 0));
  } else if (item.assetType === 'Material') {
    showToast(`Material: ${item.virtualPath}`);
  }
}

// ============================================================================
// DRAG & DROP TO 3D VIEWPORT
// ============================================================================

function setupViewportDropTarget() {
  const vp = document.getElementById('viewport');
  if (!vp) return;

  vp.addEventListener('dragover', (e) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'copy';
  });

  vp.addEventListener('drop', (e) => {
    e.preventDefault();
    const dataStr = e.dataTransfer.getData('application/json');
    if (!dataStr) return;

    try {
      const item = JSON.parse(dataStr);
      const rect = vp.getBoundingClientRect();
      const mouseX = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      const mouseY = -((e.clientY - rect.top) / rect.height) * 2 + 1;

      raycaster.setFromCamera(new THREE.Vector2(mouseX, mouseY), camera);
      const intersects = raycaster.intersectObjects(blocks, true);

      let spawnPos = new THREE.Vector3(0, 0, 0);
      if (intersects.length > 0) {
        spawnPos.copy(intersects[0].point);
      } else {
        // Fallback: ground intersection (Z=0 plane)
        const plane = new THREE.Plane(new THREE.Vector3(0, 0, 1), 0);
        raycaster.ray.intersectPlane(plane, spawnPos);
      }

      if (item.assetType === 'StaticMesh' || item.assetType === 'UsdStage') {
        requestMeshBinary(item.guid, spawnPos);
      } else {
        sendToBackend('get_asset_content', {
          guid: item.guid,
          spawnX: Math.round(spawnPos.x),
          spawnY: Math.round(spawnPos.y),
          spawnZ: Math.round(spawnPos.z)
        });
      }
      showToast(`Placing ${item.fileName} into viewport...`);
    } catch {
      // ignored
    }
  });
}

function handleSpawnAssetContent(msg) {
  if (msg.assetType === 'Scene') {
    try {
      const data = JSON.parse(msg.content);
      if (window.loadProjectFromData) {
        window.loadProjectFromData(data);
        showToast('Scene loaded: ' + msg.fileName);
      }
    } catch {
      showToast('Error parsing scene data', true);
    }
    return;
  }

  // If StaticMesh, parse and spawn custom block
  const baseName = (msg.fileName || 'Mesh').replace(/\.[^/.]+$/, '');
  const posX = msg.spawnX || 0;
  const posY = msg.spawnY || 0;
  const posZ = msg.spawnZ || 0;

  createBlock(`SM_${baseName}`, 'box', 100, 100, 100, posX, posY, posZ);
  showToast(`Placed ${baseName} at (${posX}, ${posY}, ${posZ}) cm`);
}

// ============================================================================
// CONTEXT MENU & REFERENCE VIEWER MODAL
// ============================================================================

export function handleAssetContextMenu(e, guid) {
  e.preventDefault();
  selectedAssetGuid = guid;
  contextTargetItem = (currentRegistry.assets || []).find(a => a.guid === guid);
  if (!contextTargetItem) return;

  const menu = document.getElementById('asset-context-menu');
  if (menu) {
    menu.style.display = 'block';
    menu.style.left = `${Math.min(e.clientX, window.innerWidth - 200)}px`;
    menu.style.top = `${Math.min(e.clientY, window.innerHeight - 240)}px`;
  }
}

export function hideContextMenu() {
  const menu = document.getElementById('asset-context-menu');
  if (menu) menu.style.display = 'none';
}

export function cmPlaceInScene() {
  if (contextTargetItem) {
    handleAssetDoubleClick(contextTargetItem.guid);
  }
  hideContextMenu();
}

export function cmReimport() {
  if (contextTargetItem) {
    sendToBackend('reimport_asset', { guid: contextTargetItem.guid });
  }
  hideContextMenu();
}

export function cmShowInExplorer() {
  if (contextTargetItem) {
    sendToBackend('show_in_explorer', { guid: contextTargetItem.guid });
  }
  hideContextMenu();
}

export function cmCopyVirtualPath() {
  if (contextTargetItem) {
    navigator.clipboard.writeText(contextTargetItem.virtualPath);
    showToast('Copied: ' + contextTargetItem.virtualPath);
  }
  hideContextMenu();
}

export function cmDelete() {
  if (contextTargetItem) {
    if (confirm(`Delete asset '${contextTargetItem.fileName}' and its .meta sidecar?`)) {
      sendToBackend('delete_asset', { guid: contextTargetItem.guid });
    }
  }
  hideContextMenu();
}

export function cmOpenReferenceViewer() {
  if (contextTargetItem) {
    openReferenceViewer(contextTargetItem.guid);
  }
  hideContextMenu();
}

// ============================================================================
// REFERENCE VIEWER MODAL
// ============================================================================

export function openReferenceViewer(guid) {
  const item = (currentRegistry.assets || []).find(a => a.guid === guid);
  if (!item) return;

  const modal = document.getElementById('ref-viewer-modal');
  const title = document.getElementById('ref-viewer-title');
  const body = document.getElementById('ref-viewer-body');
  if (!modal || !body) return;

  if (title) {
    title.textContent = `Reference Viewer: ${item.virtualPath}`;
  }

  const color = ASSET_TYPE_COLORS[item.assetType] || '#94a3b8';
  const icon = ASSET_TYPE_ICONS[item.assetType] || '📄';

  const referencers = item.referencers || [];
  const dependencies = item.dependencies || [];

  let html = `
    <div style="display: grid; grid-template-columns: 1fr 1.2fr 1fr; gap: 16px; align-items: start;">
      <!-- Left: Referencers ("Used By") -->
      <div style="background: #15181e; border: 1px solid #2d3748; border-radius: 6px; padding: 12px;">
        <div style="font-weight: 700; color: #60a5fa; margin-bottom: 8px; font-size: 11px; text-transform: uppercase;">
          ⬅️ Referencers (Used By: ${referencers.length})
        </div>
        ${referencers.length === 0 ? '<div style="color: #64748b; font-size: 11px;">No assets currently reference this item.</div>' : ''}
        ${referencers.map(ref => `
          <div class="ref-node-card" onclick="openReferenceViewerByPath('${ref}')">
            <span>🎬</span>
            <span style="font-weight: 600;">${ref}</span>
          </div>
        `).join('')}
      </div>

      <!-- Center: Selected Asset Node -->
      <div style="background: #1c222d; border: 2px solid ${color}; border-radius: 8px; padding: 16px; text-align: center; box-shadow: 0 4px 20px rgba(0,0,0,0.5);">
        <div style="font-size: 32px; margin-bottom: 6px;">${icon}</div>
        <div style="font-weight: 700; font-size: 13px; color: #fff;">${item.fileName}</div>
        <div style="font-size: 11px; color: ${color}; margin-bottom: 8px;">${item.virtualPath}</div>
        <div style="font-size: 10px; color: #94a3b8; font-family: monospace; background: #12151b; padding: 4px; border-radius: 4px; word-break: break-all;">
          GUID: ${item.guid}
        </div>
        <div style="margin-top: 10px; font-size: 11px; color: #cbd5e1;">
          Type: <b>${item.assetType}</b> &bull; Size: <b>${(item.fileSize / 1024).toFixed(1)} KB</b>
        </div>
      </div>

      <!-- Right: Dependencies ("Uses") -->
      <div style="background: #15181e; border: 1px solid #2d3748; border-radius: 6px; padding: 12px;">
        <div style="font-weight: 700; color: #34d399; margin-bottom: 8px; font-size: 11px; text-transform: uppercase;">
          Dependencies (Uses: ${dependencies.length}) ➡️
        </div>
        ${dependencies.length === 0 ? '<div style="color: #64748b; font-size: 11px;">No outward dependencies detected.</div>' : ''}
        ${dependencies.map(dep => `
          <div class="ref-node-card" onclick="openReferenceViewerByPath('${dep}')">
            <span>⚡</span>
            <span style="font-weight: 600;">${dep}</span>
          </div>
        `).join('')}
      </div>
    </div>
  `;

  body.innerHTML = html;
  modal.style.display = 'flex';
}

export function openReferenceViewerByPath(virtualPath) {
  const item = (currentRegistry.assets || []).find(a => a.virtualPath === virtualPath);
  if (item) {
    openReferenceViewer(item.guid);
  } else {
    showToast(`Asset '${virtualPath}' not in active registry`, true);
  }
}

export function closeReferenceViewer() {
  const modal = document.getElementById('ref-viewer-modal');
  if (modal) modal.style.display = 'none';
}

// ============================================================================
// IMPORT & PROJECT ACTIONS
// ============================================================================

export function importAssetDialog() {
  sendToBackend('import_asset', { targetFolder: activeFolder });
}

export function createProjectDialog() {
  sendToBackend('create_project');
}

export function openProjectDialog() {
  sendToBackend('open_project');
}
