// ============================================================================
// ECS SCRIPT SYSTEM (Lifecycle Hooks: onStart, onUpdate, onCollisionEnter)
// ============================================================================

import { getComponent } from '../entity.js';
import { audioSystem } from './audioSystem.js';

// Pre-registered starter script definitions
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

export class ScriptSystem {
  constructor() {
    this.elapsedTime = 0;
    this.activeInstances = new Map();
  }

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

  handleCollision(entityA, entityB) {
    this.invokeCollision(entityA, entityB);
    this.invokeCollision(entityB, entityA);
  }

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

  stop() {
    this.activeInstances.clear();
    this.elapsedTime = 0;
  }

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

export const scriptSystem = new ScriptSystem();
