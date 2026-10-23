/**
 * @file engine.js
 * @brief Three.js rendering engine core, camera controls, lighting, metric grid, and Vulkan scalability pipeline.
 */

// ============================================================================
// THREE.JS VIEWPORT & ENGINE CORE (1 Unit = 1 cm)
// ============================================================================

/** @type {HTMLElement} The DOM container element hosting the WebGL canvas. */
export const viewport = document.getElementById('viewport');

/** @type {THREE.Scene} The primary Three.js 3D scene root. */
export const scene = new THREE.Scene();
scene.background = new THREE.Color(0x121417);

/** @type {THREE.PerspectiveCamera} The primary perspective camera for the 3D viewport. */
export const camera = new THREE.PerspectiveCamera(50, viewport.clientWidth / viewport.clientHeight, 5, 50000);
camera.position.set(450, 500, 850);

/** @type {THREE.WebGLRenderer} The hardware-accelerated WebGL 2.0 renderer. */
export const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
renderer.setClearColor(0x121417, 1);
renderer.setSize(viewport.clientWidth, viewport.clientHeight);
renderer.setPixelRatio(window.devicePixelRatio);
renderer.shadowMap.enabled = true;
viewport.appendChild(renderer.domElement);

renderer.domElement.addEventListener('webglcontextlost', (e) => {
  console.warn('[Engine] WebGL Context Lost! Chromium GPU process may have reset.', e);
  e.preventDefault();
}, false);

renderer.domElement.addEventListener('webglcontextrestored', () => {
  console.log('[Engine] WebGL Context Restored! Reinitializing render pipeline...');
  renderer.setSize(viewport.clientWidth, viewport.clientHeight);
}, false);

/** @type {THREE.OrbitControls} Orbit controls for interactive editor viewport navigation. */
export const orbit = new THREE.OrbitControls(camera, renderer.domElement);
orbit.target.set(0, 100, 0);
orbit.enableDamping = true;
orbit.dampingFactor = 0.08;
orbit.update();

// ============================================================================
// TOUCHPAD & LAPTOP TRACKPAD GESTURE SUPPORT
// ============================================================================

// Prevent browser-level Ctrl+Wheel whole-page UI zoom
if (typeof window !== 'undefined') {
  window.addEventListener('wheel', (e) => {
    if (e.ctrlKey) {
      e.preventDefault();
    }
  }, { passive: false });
}

// Precision trackpad 2D Pan & Pinch-to-zoom in 3D Viewport
renderer.domElement.addEventListener('wheel', (e) => {
  e.preventDefault();

  if (e.ctrlKey) {
    // 1. PINCH-TO-ZOOM on trackpad (or Ctrl + Wheel on mouse)
    const zoomFactor = Math.pow(0.993, e.deltaY);
    camera.position.sub(orbit.target).multiplyScalar(zoomFactor).add(orbit.target);
    orbit.update();
    e.stopImmediatePropagation();
  } else if (e.shiftKey || Math.abs(e.deltaX) > 0 || (e.deltaMode === 0 && Math.abs(e.deltaY) < 35)) {
    // 2. TWO-FINGER TRACKPAD 2D PAN
    const right = new THREE.Vector3();
    const up = new THREE.Vector3();
    const forward = new THREE.Vector3();
    camera.matrixWorld.extractBasis(right, up, forward);

    const dist = camera.position.distanceTo(orbit.target);
    const panFactor = Math.max(0.08, dist * 0.0012);

    const panOffset = new THREE.Vector3();
    panOffset.addScaledVector(right, e.deltaX * panFactor);
    panOffset.addScaledVector(up, -e.deltaY * panFactor);

    camera.position.add(panOffset);
    orbit.target.add(panOffset);
    orbit.update();

    e.stopImmediatePropagation();
  }
  // Standard discrete wheel clicks pass through to OrbitControls for normal dolly
}, { passive: false, capture: true });

// Lighting
/** @type {THREE.HemisphereLight} Ambient sky/ground hemisphere light source. */
export const hemiLight = new THREE.HemisphereLight(0xffffff, 0x242a34, 0.75);
scene.add(hemiLight);

/** @type {THREE.DirectionalLight} Sun directional light source with shadow casting. */
export const dirLight = new THREE.DirectionalLight(0xffffff, 0.75);
dirLight.position.set(1200, 2200, 1400);
dirLight.castShadow = true;
scene.add(dirLight);

// Metric Centimeter Grid (40m x 40m arena)
/** @type {THREE.GridHelper} Minor 50cm metric subdivision grid helper. */
export const subGrid = new THREE.GridHelper(4000, 80, 0x333b49, 0x1f242d);
scene.add(subGrid);

/** @type {THREE.GridHelper} Major 100cm metric grid helper with orange accent lines. */
export const majorGrid = new THREE.GridHelper(4000, 40, 0xf58220, 0x3d4756);
majorGrid.position.y = 0.2;
scene.add(majorGrid);

// Ground plane for raycasting
/** @type {THREE.Mesh} Invisible infinite floor plane used for cursor raycasting intersections. */
export const groundPlane = new THREE.Mesh(
  new THREE.PlaneGeometry(20000, 20000),
  new THREE.MeshBasicMaterial({ visible: false })
);
groundPlane.rotation.x = -Math.PI / 2;
scene.add(groundPlane);

/**
 * @brief Generates a procedural 100cm prototype grid texture with 50cm subdivision borders.
 * @returns {THREE.CanvasTexture} The generated repeating texture.
 */
export function createGridTexture() {
  const canvas = document.createElement('canvas');
  canvas.width = 256; canvas.height = 256;
  const ctx = canvas.getContext('2d');
  ctx.fillStyle = '#586273'; ctx.fillRect(0, 0, 256, 256);
  ctx.fillStyle = '#4f5867'; ctx.fillRect(0, 0, 128, 128); ctx.fillRect(128, 128, 128, 128);
  ctx.strokeStyle = '#697588'; ctx.lineWidth = 2;
  ctx.strokeRect(0, 0, 128, 256); ctx.strokeRect(0, 0, 256, 128);
  ctx.strokeStyle = '#8b98ad'; ctx.lineWidth = 4;
  ctx.strokeRect(0, 0, 256, 256);
  const tex = new THREE.CanvasTexture(canvas);
  tex.wrapS = THREE.RepeatWrapping; tex.wrapT = THREE.RepeatWrapping;
  tex.anisotropy = 8;
  return tex;
}

/** @type {THREE.CanvasTexture} Active modular grid texture applied to blockout materials. */
export let gridTexture = createGridTexture();

// Scale Dummy (180 cm Dummy)
/** @type {THREE.Group} Reference mannequin group standing at 180cm human height. */
export const mannequinGroup = new THREE.Group();

/**
 * @brief Constructs the 180cm human reference dummy hierarchy and adds it to the scene.
 */
function buildMannequin() {
  const mat = new THREE.MeshStandardMaterial({ color: 0xf58220, roughness: 0.4 });
  const legGeo = new THREE.BoxGeometry(18, 90, 20);
  const lLeg = new THREE.Mesh(legGeo, mat); lLeg.position.set(-12, 45, 0);
  const rLeg = new THREE.Mesh(legGeo, mat); rLeg.position.set(12, 45, 0);
  const torso = new THREE.Mesh(new THREE.BoxGeometry(44, 60, 26), mat); torso.position.set(0, 120, 0);
  const armGeo = new THREE.BoxGeometry(12, 58, 16);
  const lArm = new THREE.Mesh(armGeo, mat); lArm.position.set(-28, 119, 0);
  const rArm = new THREE.Mesh(armGeo, mat); rArm.position.set(28, 119, 0);
  const head = new THREE.Mesh(new THREE.BoxGeometry(22, 26, 22), mat); head.position.set(0, 167, 0);
  mannequinGroup.add(lLeg, rLeg, torso, lArm, rArm, head);
  mannequinGroup.position.set(-150, 0, 150);
  mannequinGroup.name = 'Dummy_180cm';
  mannequinGroup.userData = {
    isDummy: true,
    name: '180cm Dummy',
    shapeType: 'dummy',
    sizeX: 60,
    sizeY: 30,
    sizeZ: 180
  };
  scene.add(mannequinGroup);
}
buildMannequin();

/**
 * @brief Resets the 180cm scale reference dummy back to its default scene transform coordinates.
 */
export function resetDummyTransform() {
  mannequinGroup.position.set(-150, 0, 150);
  mannequinGroup.rotation.set(0, 0, 0);
  mannequinGroup.scale.set(1, 1, 1);
  mannequinGroup.updateMatrixWorld();
}

/**
 * @brief Toggles the visibility of the 180cm scale reference dummy.
 */
export function toggleMannequin() {
  mannequinGroup.visible = !mannequinGroup.visible;
  const btn = document.getElementById('toggle-mannequin');
  if (btn) btn.classList.toggle('active', mannequinGroup.visible);
}

// Raycaster & Mouse helpers
/** @type {THREE.Raycaster} Global raycaster for viewport mouse picking. */
export const raycaster = new THREE.Raycaster();
/** @type {THREE.Vector2} Normalized device coordinates (-1 to +1) of mouse position. */
export const mouse = new THREE.Vector2();

/**
 * @brief Updates normalized device coordinates and camera ray direction from a mouse event.
 * @param {MouseEvent} e Mouse event to cast ray from.
 */
export function getMouseRay(e) {
  const rect = renderer.domElement.getBoundingClientRect();
  mouse.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
  mouse.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
  raycaster.setFromCamera(mouse, camera);
}

// ============================================================================
// MODULE 3: 4-TIER SCALABILITY SYSTEM & VULKAN ANGLE RENDERER
// ============================================================================

/**
 * @type {Array<Object>}
 * @brief 4-tier engine scalability presets tuning resolution scale, shadow maps, tone mapping, and fog.
 */
export const SCALABILITY_TIERS = [
  {
    index: 0,
    name: 'MobileLow',
    displayName: 'Tier 0 — Mobile Low (Tile-GPU Optimized)',
    shortName: 'Tier 0: Mobile Low',
    resolutionScale: 0.75,
    shadowMapSize: 1024,
    shadowType: THREE.BasicShadowMap,
    toneMapping: THREE.LinearToneMapping,
    toneMappingExposure: 1.0,
    maxShadowLights: 1,
    fogDensity: 0,
    precision: 'mediump',
    shaderModel: 'MatCap/Lambert + Baked Vertex AO',
    antiAliasing: 'None',
    targetFps: 30
  },
  {
    index: 1,
    name: 'MobileHigh',
    displayName: 'Tier 1 — Mobile High / Handheld',
    shortName: 'Tier 1: Mobile High',
    resolutionScale: 1.0,
    shadowMapSize: 1024,
    shadowType: THREE.PCFShadowMap,
    toneMapping: THREE.LinearToneMapping,
    toneMappingExposure: 1.1,
    maxShadowLights: 1,
    fogDensity: 0,
    precision: 'mediump',
    shaderModel: 'PBR-Lite + Baked Lightmaps',
    antiAliasing: 'FXAA',
    targetFps: 60
  },
  {
    index: 2,
    name: 'DesktopPC',
    displayName: 'Tier 2 — Desktop PC',
    shortName: 'Tier 2: Desktop PC',
    resolutionScale: 1.0,
    shadowMapSize: 2048,
    shadowType: THREE.PCFSoftShadowMap,
    toneMapping: THREE.ACESFilmicToneMapping,
    toneMappingExposure: 1.0,
    maxShadowLights: 4,
    fogDensity: 0.0001,
    precision: 'highp',
    shaderModel: 'Cook-Torrance PBR (highp)',
    antiAliasing: 'FXAA + ACES',
    targetFps: 60
  },
  {
    index: 3,
    name: 'Cinematic',
    displayName: 'Tier 3 — High-End PC & Console',
    shortName: 'Tier 3: Cinematic / Console',
    resolutionScale: 1.0,
    shadowMapSize: 4096,
    shadowType: THREE.PCFSoftShadowMap,
    toneMapping: THREE.ACESFilmicToneMapping,
    toneMappingExposure: 1.05,
    maxShadowLights: 8,
    fogDensity: 0.00028,
    precision: 'highp',
    shaderModel: 'Full USD Streaming PBR',
    antiAliasing: 'TAA/SMAA',
    targetFps: 120
  }
];

/** @type {number} Index of the currently active scalability tier preset (0-3). */
export let currentScalabilityTier = 2; // Default: Desktop PC

/** @type {Array<Function>} Registered subscriber callbacks invoked when scalability tier changes. */
const tierChangeCallbacks = [];

/**
 * @brief Registers a callback to be notified when the active scalability tier is adjusted.
 * @param {Function} fn Callback function receiving the active tier preset configuration.
 */
export function registerTierChangeCallback(fn) {
  if (typeof fn === 'function') tierChangeCallbacks.push(fn);
}

/**
 * @brief Configures renderer resolution, shadow maps, tone mapping, and fog based on a target scalability tier.
 * @param {number|string} tierIndex Index of the target scalability tier (0 to 3).
 * @param {boolean} [notifyBackend=true] Whether to dispatch an IPC event to the C# backend.
 */
export function setScalabilityTier(tierIndex, notifyBackend = true) {
  const idx = Math.max(0, Math.min(SCALABILITY_TIERS.length - 1, parseInt(tierIndex, 10) || 0));
  const tier = SCALABILITY_TIERS[idx];
  currentScalabilityTier = idx;

  // 1. Resolution Scale & Viewport Resize
  const ratio = window.devicePixelRatio * tier.resolutionScale;
  renderer.setPixelRatio(ratio);
  renderer.setSize(viewport.clientWidth, viewport.clientHeight);

  // 2. Shadows Configuration
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = tier.shadowType;
  if (dirLight && dirLight.shadow) {
    dirLight.shadow.mapSize.width = tier.shadowMapSize;
    dirLight.shadow.mapSize.height = tier.shadowMapSize;
    if (dirLight.shadow.map) {
      dirLight.shadow.map.dispose();
      dirLight.shadow.map = null;
    }
  }

  // 3. Tone Mapping & Color Pipeline
  renderer.toneMapping = tier.toneMapping;
  renderer.toneMappingExposure = tier.toneMappingExposure;

  // 4. Volumetric / Atmospheric Fog
  if (tier.fogDensity > 0) {
    scene.fog = new THREE.FogExp2(0x121417, tier.fogDensity);
  } else {
    scene.fog = null;
  }

  // 5. Notify registered subsystem callbacks (e.g. lights manager)
  for (const cb of tierChangeCallbacks) {
    try { cb(tier); } catch (e) { console.warn('Tier callback error:', e); }
  }

  // 6. Update UI dropdown if present
  const select = document.getElementById('scalability-tier-select');
  if (select && select.value !== String(idx)) {
    select.value = String(idx);
  }

  // 7. Send IPC notification to .NET backend
  if (notifyBackend && window.external && window.external.sendMessage) {
    try {
      window.external.sendMessage(JSON.stringify({
        action: 'set_scalability_tier',
        tierIndex: tier.index,
        tierName: tier.name
      }));
    } catch { }
  }

  updateGpuHud(true);
}

// ============================================================================
// GPU INSTANCING & STATIC GEOMETRY BATCHING
// ============================================================================

/** @type {Array<THREE.InstancedMesh>} Active instanced mesh batches rendered in the scene. */
export const instancedMeshBatches = [];

/**
 * @brief Removes all active GPU instanced mesh batches and disposes their resources.
 */
export function clearInstancedBatches() {
  for (const batch of instancedMeshBatches) {
    scene.remove(batch);
    if (batch.geometry) batch.geometry.dispose();
    if (batch.material) batch.material.dispose();
  }
  instancedMeshBatches.length = 0;
}

/**
 * @brief Creates a batched Three.js InstancedMesh for high-performance rendering of repetitive geometries.
 * @param {THREE.BufferGeometry} geometry Shared base geometry.
 * @param {THREE.Material} material Shared material instance.
 * @param {Array<Object>} transforms Array of transform parameters for each instance.
 * @returns {THREE.InstancedMesh|null} The created instanced mesh, or null if transforms is empty.
 */
export function createInstancedMesh(geometry, material, transforms) {
  if (!transforms || transforms.length === 0) return null;
  const count = transforms.length;
  const instMesh = new THREE.InstancedMesh(geometry, material, count);
  instMesh.castShadow = true;
  instMesh.receiveShadow = true;

  const dummy = new THREE.Object3D();
  for (let i = 0; i < count; i++) {
    const t = transforms[i];
    dummy.position.set(t.posX || 0, t.posY || 0, t.posZ || 0);
    dummy.rotation.set(t.rotX || 0, t.rotY || 0, t.rotZ || 0);
    dummy.scale.set(t.scaleX || 1, t.scaleY || 1, t.scaleZ || 1);
    dummy.updateMatrix();
    instMesh.setMatrixAt(i, dummy.matrix);
  }
  instMesh.instanceMatrix.needsUpdate = true;
  instancedMeshBatches.push(instMesh);
  scene.add(instMesh);
  return instMesh;
}

// ============================================================================
// REAL-TIME GPU & VULKAN STATUS HUD (F3)
// ============================================================================

/** @type {boolean} Indicates whether the real-time GPU statistics HUD is visible. */
let hudVisible = false;
/** @type {number} Timestamp in milliseconds of the last FPS counter sample. */
let lastFpsTime = performance.now();
/** @type {number} Number of frames rendered since the last FPS calculation. */
let frameCount = 0;
/** @type {number} Current computed frames-per-second rate. */
let currentFps = 60;
/** @type {string} Current frame rendering time in milliseconds. */
let currentFrameTime = '16.60';
/** @type {string} Unmasked GPU hardware vendor string. */
let gpuVendor = '';
/** @type {string} Unmasked GPU renderer / driver string. */
let gpuRenderer = '';

/**
 * @brief Queries unmasked GPU hardware and driver strings via the WEBGL_debug_renderer_info extension.
 */
export function initGpuRendererInfo() {
  try {
    const gl = renderer.getContext();
    const dbg = gl.getExtension('WEBGL_debug_renderer_info');
    if (dbg) {
      gpuVendor = gl.getParameter(dbg.UNMASKED_VENDOR_WEBGL) || 'Vulkan / GPU';
      gpuRenderer = gl.getParameter(dbg.UNMASKED_RENDERER_WEBGL) || 'Vulkan ANGLE WebGL 2.0';
    } else {
      gpuRenderer = 'WebGL 2.0 Host';
    }
  } catch (e) {
    gpuRenderer = 'WebGL 2.0 (Standard)';
  }
}

/**
 * @brief Toggles the visibility of the on-screen GPU and Vulkan performance HUD overlay.
 */
export function toggleGpuHud() {
  hudVisible = !hudVisible;
  const hudEl = document.getElementById('gpu-hud');
  if (hudEl) {
    hudEl.style.display = hudVisible ? 'block' : 'none';
  }
  const btn = document.getElementById('toggle-gpu-hud-btn');
  if (btn) btn.classList.toggle('active', hudVisible);
}

/**
 * @brief Checks if the GPU status HUD overlay is currently visible.
 * @returns {boolean} True if the HUD is visible; otherwise false.
 */
export function isGpuHudVisible() {
  return hudVisible;
}

/**
 * @brief Updates FPS, frame times, draw calls, triangle counts, and memory usage in the GPU HUD.
 * @param {boolean} [force=false] Force an immediate HUD redraw regardless of the elapsed interval.
 */
export function updateGpuHud(force = false) {
  frameCount++;
  const now = performance.now();
  const delta = now - lastFpsTime;

  if (force || delta >= 250) {
    if (delta > 0) {
      currentFps = Math.max(1, Math.round((frameCount * 1000) / delta));
      currentFrameTime = (delta / frameCount).toFixed(2);
    }
    frameCount = 0;
    lastFpsTime = now;

    const hudEl = document.getElementById('gpu-hud');
    if (hudEl && hudVisible) {
      const tier = SCALABILITY_TIERS[currentScalabilityTier] || SCALABILITY_TIERS[2];
      const calls = renderer.info.render.calls;
      const tris = renderer.info.render.triangles;
      const geos = renderer.info.memory.geometries;
      const texs = renderer.info.memory.textures;
      const instCount = instancedMeshBatches.length;

      let fpsColor = '#2ecc71';
      if (currentFps < 30) fpsColor = '#e04848';
      else if (currentFps < 50) fpsColor = '#f58220';

      hudEl.innerHTML = `
        <div class="hud-header">
          <span class="hud-title">⚡ GPU & VULKAN STATUS</span>
          <span class="hud-close" onclick="toggleGpuHud()">✕</span>
        </div>
        <div class="hud-row"><span class="hud-lbl">FPS:</span> <span class="hud-val" style="color:${fpsColor}; font-weight:700;">${currentFps} FPS</span> <span class="hud-sub">(${currentFrameTime} ms)</span></div>
        <div class="hud-row"><span class="hud-lbl">Draw Calls:</span> <span class="hud-val">${calls}</span> &nbsp;|&nbsp; <span class="hud-lbl">Triangles:</span> <span class="hud-val">${tris.toLocaleString()}</span></div>
        <div class="hud-row"><span class="hud-lbl">Geometries:</span> <span class="hud-val">${geos}</span> &nbsp;|&nbsp; <span class="hud-lbl">Textures:</span> <span class="hud-val">${texs}</span></div>
        <div class="hud-row"><span class="hud-lbl">Instanced Batches:</span> <span class="hud-val">${instCount}</span></div>
        <div class="hud-divider"></div>
        <div class="hud-row"><span class="hud-lbl">Active Tier:</span> <span class="hud-val" style="color:var(--ue-orange, #f58220); font-weight:600;">${tier.displayName}</span></div>
        <div class="hud-row"><span class="hud-lbl">Resolution Scale:</span> <span class="hud-val">${tier.resolutionScale * 100}% (${Math.round(viewport.clientWidth * tier.resolutionScale)}&times;${Math.round(viewport.clientHeight * tier.resolutionScale)})</span></div>
        <div class="hud-row"><span class="hud-lbl">Shadow Map:</span> <span class="hud-val">${tier.shadowMapSize}px (${tier.shadowType === THREE.BasicShadowMap ? 'Basic' : 'PCFSoft'})</span></div>
        <div class="hud-row"><span class="hud-lbl">Shading:</span> <span class="hud-val">${tier.shaderModel}</span></div>
        <div class="hud-divider"></div>
        <div class="hud-row"><span class="hud-lbl">Host Backend:</span> <span class="hud-val" style="color:#0078d4;">Vulkan ANGLE WebGL 2.0</span></div>
        <div class="hud-row"><span class="hud-lbl">Compute Baker:</span> <span class="hud-val" style="color:#38bdf8;">Vulkan SDK (SPIR-V) / .NET 10 SIMD</span></div>
        <div class="hud-gpu-name" title="${gpuRenderer}">${gpuRenderer || 'Vulkan ANGLE Adapter'}</div>
      `;
    }
  }
}

// Initial probe
initGpuRendererInfo();

// F3 Hotkey toggle for HUD
window.addEventListener('keydown', (e) => {
  if (e.key === 'F3') {
    e.preventDefault();
    toggleGpuHud();
  }
});
