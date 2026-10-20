/**
 * @file scriptSystem.js
 * @brief ECS Script System managing entity script lifecycles (onStart, onUpdate, onCollisionEnter).
 */

// ============================================================================
// ECS SCRIPT SYSTEM (Lifecycle Hooks: onStart, onUpdate, onCollisionEnter)
// ============================================================================

import { getComponent } from '../entity.js';
import { audioSystem } from './audioSystem.js';

/**
 * @brief Registry of built-in script definitions providing gameplay behavior presets.
 * @type {Object.<string, {onStart?: Function, onUpdate?: Function, onCollisionEnter?: Function}>}
 */
export const BuiltinScripts = {
  'Rotator.js': {
    onStart(entity, ctx) {
      this.speed = ctx.properties.speed || 45; // deg/sec
    },
    onUpdate(entity, dt, ctx) {
      const rad = THREE.MathUtils.degToRad(this.speed * dt);
      entity.rotation.y += rad;
    },
    onCollisionEnter(entity, other, ctx) {
      ctx.playSound('impact');
    }
  },

  'DoorTrigger.js': {
    onStart(entity, ctx) {
      this.isOpen = false;
      this.initialY = entity.position.y;
      this.targetY = this.initialY + 250; // Slide open 250 cm
    },
    onUpdate(entity, dt, ctx) {
      const dest = this.isOpen ? this.targetY : this.initialY;
      entity.position.y += (dest - entity.position.y) * Math.min(1.0, 5.0 * dt);
    },
    onCollisionEnter(entity, other, ctx) {
      this.isOpen = !this.isOpen;
      ctx.playSound('chime');
    }
  },

  'PlayerMovement.js': {
    onStart(entity, ctx) {},
    onUpdate(entity, dt, ctx) {},
    onCollisionEnter(entity, other, ctx) {
      ctx.playSound('footstep');
    }
  }
};

/**
 * @class ScriptSystem
 * @brief Coordinates script lifecycle execution across scene entities attached with a ScriptComponent.
 */
export class ScriptSystem {
  /**
   * @brief Initializes a new instance of the ScriptSystem class.
   */
  constructor() {
    /** @type {number} Total elapsed play simulation time in seconds. */
    this.elapsedTime = 0;
    /** @type {Map<THREE.Object3D, {instance: Object, scriptComp: Object}>} Map of active entity script instances. */
    this.activeInstances = new Map();
  }

  /**
   * @brief Discovers and initializes all active entity scripts at the start of Play Mode.
   * @param {Array<THREE.Object3D>} entities Array of scene entities.
   */
  start(entities) {
    this.elapsedTime = 0;
    this.activeInstances.clear();

    for (const entity of entities) {
      const scriptComp = getComponent(entity, 'Script');
      if (!scriptComp || !scriptComp.enabled) continue;

      const path = scriptComp.scriptPath || 'Rotator.js';
      const scriptDef = BuiltinScripts[path] || BuiltinScripts['Rotator.js'];

      // Create instance
      const instance = { ...scriptDef };
      const ctx = this.createContext(entity, scriptComp.properties);

      if (typeof instance.onStart === 'function') {
        try {
          instance.onStart(entity, ctx);
        } catch (e) {
          console.error(`Error in onStart of ${path}:`, e);
        }
      }

      this.activeInstances.set(entity, { instance, scriptComp });
    }
  }

  /**
   * @brief Advances script logic each frame, calling onUpdate on all active script instances.
   * @param {number} dt Delta time in seconds since the last simulation step.
   * @param {Set<string>|null} [keyboardState=null] Active set of pressed keyboard key codes.
   */
  update(dt, keyboardState = null) {
    this.elapsedTime += dt;

    for (const [entity, { instance, scriptComp }] of this.activeInstances.entries()) {
      if (!scriptComp.enabled) continue;
      const ctx = this.createContext(entity, scriptComp.properties, keyboardState, dt);

      if (typeof instance.onUpdate === 'function') {
        try {
          instance.onUpdate(entity, dt, ctx);
        } catch (e) {
          console.error('Error in onUpdate of script:', e);
        }
      }
    }
  }

  /**
   * @brief Dispatches collision notifications symmetrically to both interacting entities.
   * @param {THREE.Object3D} entityA First interacting entity.
   * @param {THREE.Object3D} entityB Second interacting entity.
   */
  handleCollision(entityA, entityB) {
    this.invokeCollision(entityA, entityB);
    this.invokeCollision(entityB, entityA);
  }

  /**
   * @brief Dispatches onCollisionEnter callback to an entity's attached script.
   * @param {THREE.Object3D} entity Target entity whose script will receive the event.
   * @param {THREE.Object3D} other The other colliding entity.
   */
  invokeCollision(entity, other) {
    const entry = this.activeInstances.get(entity);
    if (!entry) return;
    const { instance, scriptComp } = entry;
    if (!scriptComp.enabled || typeof instance.onCollisionEnter !== 'function') return;

    const ctx = this.createContext(entity, scriptComp.properties);
    try {
      instance.onCollisionEnter(entity, other, ctx);
    } catch (e) {
      console.error('Error in onCollisionEnter:', e);
    }
  }

  /**
   * @brief Stops all running scripts and clears active script instances when exiting Play Mode.
   */
  stop() {
    this.activeInstances.clear();
    this.elapsedTime = 0;
  }

  /**
   * @brief Constructs an isolated execution context exposed to script lifecycle hooks.
   * @param {THREE.Object3D} entity The host entity.
   * @param {Object} [properties={}] User-defined inspector properties.
   * @param {Set<string>|null} [keyboardState=null] Active keyboard keys.
   * @param {number} [dt=0.016] Current delta time in seconds.
   * @returns {Object} Execution context object containing runtime utilities and helpers.
   */
  createContext(entity, properties = {}, keyboardState = null, dt = 0.016) {
    return {
      entity,
      dt,
      time: this.elapsedTime,
      properties: { ...properties },
      isKeyDown: (code) => keyboardState ? keyboardState.has(code) : false,
      playSound: (clipName) => audioSystem.playSound(clipName, entity.position),
      callBackend: (action, payload) => {
        if (window.external && window.external.sendMessage) {
          window.external.sendMessage(JSON.stringify({ action, ...payload }));
        }
      }
    };
  }
}

/**
 * @type {ScriptSystem}
 * @brief Singleton instance of the ScriptSystem.
 */
export const scriptSystem = new ScriptSystem();
