/**
 * @file components.js
 * @brief Component definitions and factory constructor for the Entity Component System.
 */

/**
 * Component defining spatial position, rotation, and scale in engine coordinates (Z-Up Centimeters).
 */
export class TransformComponent {
  /**
   * @param {Object} [data={}] - Initial component data.
   */
  constructor(data = {}) {
    this.type = 'Transform';
    this.posX = data.posX ?? 0;
    this.posY = data.posY ?? 0;
    this.posZ = data.posZ ?? 0;
    this.rotX = data.rotX ?? 0;
    this.rotY = data.rotY ?? 0;
    this.rotZ = data.rotZ ?? 0;
    this.scaleX = data.scaleX ?? 1;
    this.scaleY = data.scaleY ?? 1;
    this.scaleZ = data.scaleZ ?? 1;
  }

  /**
   * Serializes component properties.
   * @returns {Object} Plain JavaScript object representation.
   */
  serialize() {
    return {
      type: this.type,
      posX: this.posX, posY: this.posY, posZ: this.posZ,
      rotX: this.rotX, rotY: this.rotY, rotZ: this.rotZ,
      scaleX: this.scaleX, scaleY: this.scaleY, scaleZ: this.scaleZ
    };
  }
}

/**
 * Component binding an entity to 3D mesh geometry, PBR material properties, or external assets.
 */
export class MeshComponent {
  /**
   * @param {Object} [data={}] - Initial component data.
   */
  constructor(data = {}) {
    this.type = 'Mesh';
    this.shapeType = data.shapeType || 'box';
    this.sizeX = data.sizeX ?? 100;
    this.sizeY = data.sizeY ?? 100;
    this.sizeZ = data.sizeZ ?? 100;
    this.assetGuid = data.assetGuid || null;
    this.virtualPath = data.virtualPath || null;
    this.materialGuid = data.materialGuid || null;
    this.color = data.color || '#586273';
    this.roughness = data.roughness ?? 0.7;
    this.metallic = data.metallic ?? 0.1;
  }

  /**
   * Serializes component properties.
   * @returns {Object} Plain JavaScript object representation.
   */
  serialize() {
    return {
      type: this.type,
      shapeType: this.shapeType,
      sizeX: this.sizeX, sizeY: this.sizeY, sizeZ: this.sizeZ,
      assetGuid: this.assetGuid,
      virtualPath: this.virtualPath,
      materialGuid: this.materialGuid,
      color: this.color,
      roughness: this.roughness,
      metallic: this.metallic
    };
  }
}

/**
 * Component providing collision bounds (Box, Sphere, Capsule) or trigger event zones.
 */
export class ColliderComponent {
  /**
   * @param {Object} [data={}] - Initial component data.
   */
  constructor(data = {}) {
    this.type = 'Collider';
    this.colliderType = data.colliderType || 'box'; // 'box', 'sphere', 'capsule', 'mesh'
    this.sizeX = data.sizeX ?? 100;
    this.sizeY = data.sizeY ?? 100;
    this.sizeZ = data.sizeZ ?? 100;
    this.radius = data.radius ?? 50;
    this.height = data.height ?? 180;
    this.centerOffsetX = data.centerOffsetX ?? 0;
    this.centerOffsetY = data.centerOffsetY ?? 0;
    this.centerOffsetZ = data.centerOffsetZ ?? 0;
    this.isTrigger = !!data.isTrigger;
  }

  /**
   * Serializes component properties.
   * @returns {Object} Plain JavaScript object representation.
   */
  serialize() {
    return {
      type: this.type,
      colliderType: this.colliderType,
      sizeX: this.sizeX, sizeY: this.sizeY, sizeZ: this.sizeZ,
      radius: this.radius, height: this.height,
      centerOffsetX: this.centerOffsetX, centerOffsetY: this.centerOffsetY, centerOffsetZ: this.centerOffsetZ,
      isTrigger: this.isTrigger
    };
  }
}

/**
 * Component defining physical mass, gravity response, and linear velocity.
 */
export class RigidbodyComponent {
  /**
   * @param {Object} [data={}] - Initial component data.
   */
  constructor(data = {}) {
    this.type = 'Rigidbody';
    this.mobility = data.mobility || 'Static'; // 'Static', 'Kinematic', 'Dynamic'
    this.mass = data.mass ?? 50; // kg
    this.useGravity = data.useGravity !== undefined ? data.useGravity : true;
    this.drag = data.drag ?? 0.05;
    this.velocityX = data.velocityX ?? 0;
    this.velocityY = data.velocityY ?? 0;
    this.velocityZ = data.velocityZ ?? 0;
  }

  /**
   * Serializes component properties.
   * @returns {Object} Plain JavaScript object representation.
   */
  serialize() {
    return {
      type: this.type,
      mobility: this.mobility,
      mass: this.mass,
      useGravity: this.useGravity,
      drag: this.drag,
      velocityX: this.velocityX, velocityY: this.velocityY, velocityZ: this.velocityZ
    };
  }
}

/**
 * Component providing first-person kinematic movement, collision sliding, and mouse-look.
 */
export class CharacterControllerComponent {
  /**
   * @param {Object} [data={}] - Initial component data.
   */
  constructor(data = {}) {
    this.type = 'CharacterController';
    this.height = data.height ?? 180; // 180 cm (180cm Dummy reference height)
    this.radius = data.radius ?? 30;  // 30 cm (60 cm total width)
    this.stepHeight = data.stepHeight ?? 45; // 45 cm max step climbing
    this.walkSpeed = data.walkSpeed ?? 450;   // 450 cm/s
    this.sprintSpeed = data.sprintSpeed ?? 750; // 750 cm/s
    this.jumpVelocity = data.jumpVelocity ?? 600; // 600 cm/s
    this.mouseSensitivity = data.mouseSensitivity ?? 0.0022;
    this.isGrounded = data.isGrounded !== undefined ? data.isGrounded : true;
    this.pitch = data.pitch ?? 0;
    this.yaw = data.yaw ?? 0;
    this.velocityX = 0;
    this.velocityY = 0;
    this.velocityZ = 0;
  }

  /**
   * Serializes component properties.
   * @returns {Object} Plain JavaScript object representation.
   */
  serialize() {
    return {
      type: this.type,
      height: this.height,
      radius: this.radius,
      stepHeight: this.stepHeight,
      walkSpeed: this.walkSpeed,
      sprintSpeed: this.sprintSpeed,
      jumpVelocity: this.jumpVelocity,
      mouseSensitivity: this.mouseSensitivity,
      isGrounded: this.isGrounded,
      pitch: this.pitch,
      yaw: this.yaw
    };
  }
}

/**
 * Component attaching custom behavioral scripts with lifecycle hooks (onStart, onUpdate).
 */
export class ScriptComponent {
  /**
   * @param {Object} [data={}] - Initial component data.
   */
  constructor(data = {}) {
    this.type = 'Script';
    this.scriptPath = data.scriptPath || 'Rotator.js';
    this.scriptSource = data.scriptSource || null;
    this.properties = data.properties ? { ...data.properties } : { speed: 45 };
    this.enabled = data.enabled !== undefined ? data.enabled : true;
    this.instance = null;
  }

  /**
   * Serializes component properties.
   * @returns {Object} Plain JavaScript object representation.
   */
  serialize() {
    return {
      type: this.type,
      scriptPath: this.scriptPath,
      scriptSource: this.scriptSource,
      properties: { ...this.properties },
      enabled: this.enabled
    };
  }
}

/**
 * Component providing 3D spatialized or ambient audio playback.
 */
export class AudioSourceComponent {
  /**
   * @param {Object} [data={}] - Initial component data.
   */
  constructor(data = {}) {
    this.type = 'AudioSource';
    this.clipPath = data.clipPath || 'footstep'; // preset or audio asset path
    this.volume = data.volume ?? 0.8;
    this.loop = !!data.loop;
    this.playOnStart = !!data.playOnStart;
    this.isSpatial = data.isSpatial !== undefined ? data.isSpatial : true;
    this.maxDistance = data.maxDistance ?? 3000; // 3000 cm = 30m
  }

  /**
   * Serializes component properties.
   * @returns {Object} Plain JavaScript object representation.
   */
  serialize() {
    return {
      type: this.type,
      clipPath: this.clipPath,
      volume: this.volume,
      loop: this.loop,
      playOnStart: this.playOnStart,
      isSpatial: this.isSpatial,
      maxDistance: this.maxDistance
    };
  }
}

/**
 * Instantiates an ECS component by type identifier.
 * @param {string} type - Component type name.
 * @param {Object} [data={}] - Component property data.
 * @returns {Object|null} Created component instance or null.
 */
export function createComponentByType(type, data = {}) {
  switch (type) {
    case 'Transform': return new TransformComponent(data);
    case 'Mesh': return new MeshComponent(data);
    case 'Collider': return new ColliderComponent(data);
    case 'Rigidbody': return new RigidbodyComponent(data);
    case 'CharacterController': return new CharacterControllerComponent(data);
    case 'Script': return new ScriptComponent(data);
    case 'AudioSource': return new AudioSourceComponent(data);
    default:
      console.warn('Unknown component type:', type);
      return null;
  }
}
