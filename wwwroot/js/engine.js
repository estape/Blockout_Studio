// ============================================================================
// THREE.JS VIEWPORT & ENGINE CORE (1 Unit = 1 cm)
// ============================================================================

export const viewport = document.getElementById('viewport');
export const scene = new THREE.Scene();
scene.background = new THREE.Color(0x121417);

export const camera = new THREE.PerspectiveCamera(50, viewport.clientWidth / viewport.clientHeight, 5, 50000);
camera.position.set(450, 500, 850);

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

export const orbit = new THREE.OrbitControls(camera, renderer.domElement);
orbit.target.set(0, 100, 0);
orbit.enableDamping = true;
orbit.dampingFactor = 0.08;
orbit.update();

// Lighting
export const hemiLight = new THREE.HemisphereLight(0xffffff, 0x242a34, 0.75);
scene.add(hemiLight);

export const dirLight = new THREE.DirectionalLight(0xffffff, 0.75);
dirLight.position.set(1200, 2200, 1400);
dirLight.castShadow = true;
scene.add(dirLight);

// Metric Centimeter Grid (40m x 40m arena)
export const subGrid = new THREE.GridHelper(4000, 80, 0x333b49, 0x1f242d);
scene.add(subGrid);
export const majorGrid = new THREE.GridHelper(4000, 40, 0xf58220, 0x3d4756);
majorGrid.position.y = 0.2;
scene.add(majorGrid);

// Ground plane for raycasting
export const groundPlane = new THREE.Mesh(
  new THREE.PlaneGeometry(20000, 20000),
  new THREE.MeshBasicMaterial({ visible: false })
);
groundPlane.rotation.x = -Math.PI / 2;
scene.add(groundPlane);

// Procedural Modular Grid Texture (100 cm tile with 50 cm subdivisions)
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
export let gridTexture = createGridTexture();

// Scale Dummy (180 cm Dummy)
export const mannequinGroup = new THREE.Group();
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

export function resetDummyTransform() {
  mannequinGroup.position.set(-150, 0, 150);
  mannequinGroup.rotation.set(0, 0, 0);
  mannequinGroup.scale.set(1, 1, 1);
  mannequinGroup.updateMatrixWorld();
}

export function toggleMannequin() {
  mannequinGroup.visible = !mannequinGroup.visible;
  const btn = document.getElementById('toggle-mannequin');
  if (btn) btn.classList.toggle('active', mannequinGroup.visible);
}

// Raycaster & Mouse helpers
export const raycaster = new THREE.Raycaster();
export const mouse = new THREE.Vector2();

export function getMouseRay(e) {
  const rect = renderer.domElement.getBoundingClientRect();
  mouse.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
  mouse.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
  raycaster.setFromCamera(mouse, camera);
}

// ============================================================================
// MODULE 3: 4-TIER SCALABILITY SYSTEM & VULKAN ANGLE RENDERER
// ============================================================================

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

export let currentScalabilityTier = 2; // Default: Desktop PC
const tierChangeCallbacks = [];

export function registerTierChangeCallback(fn) {
  if (typeof fn === 'function') tierChangeCallbacks.push(fn);
}

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

export const instancedMeshBatches = [];

export function clearInstancedBatches() {
  for (const batch of instancedMeshBatches) {
    scene.remove(batch);
    if (batch.geometry) batch.geometry.dispose();
    if (batch.material) batch.material.dispose();
  }
  instancedMeshBatches.length = 0;
}

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

let hudVisible = false;
let lastFpsTime = performance.now();
let frameCount = 0;
let currentFps = 60;
let currentFrameTime = '16.60';
let gpuVendor = '';
let gpuRenderer = '';

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

export function toggleGpuHud() {
  hudVisible = !hudVisible;
  const hudEl = document.getElementById('gpu-hud');
  if (hudEl) {
    hudEl.style.display = hudVisible ? 'block' : 'none';
  }
  const btn = document.getElementById('toggle-gpu-hud-btn');
  if (btn) btn.classList.toggle('active', hudVisible);
}

export function isGpuHudVisible() {
  return hudVisible;
}

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


