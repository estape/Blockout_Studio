/**
 * @file audioSystem.js
 * @brief ECS Audio System providing 3D spatial WebAudio API integration and procedural sound synthesis.
 */

// ============================================================================
// ECS AUDIO SYSTEM (3D Spatial WebAudio API & Procedural Sound Bank)
// ============================================================================

/**
 * @class AudioSystem
 * @brief Manages WebAudio context, listener spatial coordinates, and procedural audio synthesis.
 */
export class AudioSystem {
  /**
   * @brief Initializes a new instance of the AudioSystem class.
   */
  constructor() {
    /** @type {AudioContext|null} The underlying WebAudio context. */
    this.ctx = null;
    /** @type {GainNode|null} Master gain node for overall volume control. */
    this.masterGain = null;
    /** @type {boolean} Indicates whether all engine audio is currently muted. */
    this.isMuted = false;
  }

  /**
   * @brief Ensures that the WebAudio context is created and resumed.
   * @returns {AudioContext|null} The active WebAudio context, or null if unsupported.
   */
  ensureContext() {
    if (!this.ctx) {
      const AudioCtx = window.AudioContext || window.webkitAudioContext;
      if (AudioCtx) {
        this.ctx = new AudioCtx();
        this.masterGain = this.ctx.createGain();
        this.masterGain.gain.value = 0.8;
        this.masterGain.connect(this.ctx.destination);
      }
    }
    if (this.ctx && this.ctx.state === 'suspended') {
      this.ctx.resume();
    }
    return this.ctx;
  }

  /**
   * @brief Updates the 3D listener position and orientation to match the active camera.
   * @param {THREE.Camera} camera The Three.js camera representing the listener's viewpoint.
   */
  updateListener(camera) {
    if (!this.ctx || !camera) return;
    const l = this.ctx.listener;
    const pos = camera.position;

    // Camera forward vector
    const forward = new THREE.Vector3(0, 0, -1).applyQuaternion(camera.quaternion);
    const up = new THREE.Vector3(0, 1, 0).applyQuaternion(camera.quaternion);

    if (l.positionX) {
      l.positionX.setValueAtTime(pos.x / 100, this.ctx.currentTime); // Convert cm to meters for audio
      l.positionY.setValueAtTime(pos.y / 100, this.ctx.currentTime);
      l.positionZ.setValueAtTime(pos.z / 100, this.ctx.currentTime);
      l.forwardX.setValueAtTime(forward.x, this.ctx.currentTime);
      l.forwardY.setValueAtTime(forward.y, this.ctx.currentTime);
      l.forwardZ.setValueAtTime(forward.z, this.ctx.currentTime);
      l.upX.setValueAtTime(up.x, this.ctx.currentTime);
      l.upY.setValueAtTime(up.y, this.ctx.currentTime);
      l.upZ.setValueAtTime(up.z, this.ctx.currentTime);
    } else {
      l.setPosition(pos.x / 100, pos.y / 100, pos.z / 100);
      l.setOrientation(forward.x, forward.y, forward.z, up.x, up.y, up.z);
    }
  }

  /**
   * @brief Plays a procedural sound clip with optional 3D HRTF spatialization.
   * @param {string} [clipName='chime'] Name of the sound preset ('jump', 'footstep', 'impact', or 'chime').
   * @param {THREE.Vector3|null} [position=null] World position of the sound emitter in centimeters.
   * @param {number} [volume=0.7] Linear gain multiplier for playback volume.
   */
  playSound(clipName = 'chime', position = null, volume = 0.7) {
    const ctx = this.ensureContext();
    if (!ctx) return;

    const t = ctx.currentTime;
    let nodeToConnect = this.masterGain;

    // Optional 3D HRTF Panner
    if (position) {
      const panner = ctx.createPanner();
      panner.panningModel = 'HRTF';
      panner.distanceModel = 'inverse';
      panner.refDistance = 1.0;  // 1 meter = 100 cm
      panner.maxDistance = 30.0; // 30 meters = 3000 cm
      panner.rolloffFactor = 1.0;

      const px = position.x / 100;
      const py = position.y / 100;
      const pz = position.z / 100;

      if (panner.positionX) {
        panner.positionX.setValueAtTime(px, t);
        panner.positionY.setValueAtTime(py, t);
        panner.positionZ.setValueAtTime(pz, t);
      } else {
        panner.setPosition(px, py, pz);
      }

      panner.connect(this.masterGain);
      nodeToConnect = panner;
    }

    const gain = ctx.createGain();
    gain.gain.setValueAtTime(volume, t);
    gain.connect(nodeToConnect);

    // Procedural sound synthesis presets
    if (clipName === 'jump') {
      const osc = ctx.createOscillator();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(150, t);
      osc.frequency.exponentialRampToValueAtTime(450, t + 0.18);
      gain.gain.setValueAtTime(volume, t);
      gain.gain.linearRampToValueAtTime(0.001, t + 0.22);
      osc.connect(gain);
      osc.start(t);
      osc.stop(t + 0.22);
    } else if (clipName === 'footstep') {
      const osc = ctx.createOscillator();
      osc.type = 'triangle';
      osc.frequency.setValueAtTime(90, t);
      osc.frequency.exponentialRampToValueAtTime(40, t + 0.08);
      gain.gain.setValueAtTime(volume * 0.4, t);
      gain.gain.linearRampToValueAtTime(0.001, t + 0.09);
      osc.connect(gain);
      osc.start(t);
      osc.stop(t + 0.09);
    } else if (clipName === 'impact') {
      const osc = ctx.createOscillator();
      osc.type = 'square';
      osc.frequency.setValueAtTime(120, t);
      osc.frequency.exponentialRampToValueAtTime(30, t + 0.15);
      gain.gain.setValueAtTime(volume * 0.5, t);
      gain.gain.exponentialRampToValueAtTime(0.001, t + 0.18);
      osc.connect(gain);
      osc.start(t);
      osc.stop(t + 0.18);
    } else {
      // Default: 'chime' (Two harmonious sine tones C5 + E5)
      const osc1 = ctx.createOscillator();
      const osc2 = ctx.createOscillator();
      osc1.type = 'sine'; osc2.type = 'sine';
      osc1.frequency.setValueAtTime(523.25, t); // C5
      osc2.frequency.setValueAtTime(659.25, t + 0.05); // E5

      gain.gain.setValueAtTime(volume * 0.6, t);
      gain.gain.exponentialRampToValueAtTime(0.001, t + 0.35);

      osc1.connect(gain);
      osc2.connect(gain);
      osc1.start(t); osc1.stop(t + 0.35);
      osc2.start(t + 0.05); osc2.stop(t + 0.35);
    }
  }
}

/**
 * @type {AudioSystem}
 * @brief Singleton instance of the AudioSystem.
 */
export const audioSystem = new AudioSystem();
