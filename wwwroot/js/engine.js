// ============================================================================
// THREE.JS VIEWPORT & ENGINE CORE (1 Unit = 1 cm)
// ============================================================================

export const viewport = document.getElementById('viewport');
export const scene = new THREE.Scene();
scene.background = new THREE.Color(0x121417);

export const camera = new THREE.PerspectiveCamera(50, viewport.clientWidth / viewport.clientHeight, 5, 50000);
camera.position.set(450, 500, 850);

export const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
renderer.setSize(viewport.clientWidth, viewport.clientHeight);
renderer.setPixelRatio(window.devicePixelRatio);
renderer.shadowMap.enabled = true;
viewport.appendChild(renderer.domElement);

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

// UE5 Centimeter Grid (40m x 40m arena)
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

// Procedural UE5 Grid Texture (100 cm tile with 50 cm subdivisions)
export function createUE5GridTexture() {
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
export let gridTexture = createUE5GridTexture();

// Scale Dummy (180 cm Mannequin)
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
  scene.add(mannequinGroup);
}
buildMannequin();

export function toggleMannequin() {
  mannequinGroup.visible = !mannequinGroup.visible;
  document.getElementById('toggle-mannequin').classList.toggle('active', mannequinGroup.visible);
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

