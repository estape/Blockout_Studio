/**
 * @file viewportSync.js
 * @brief Dynamic viewport docking and resize synchronizer connecting DOM layout to EngineNative Vulkan.
 */

class ViewportSynchronizer {
  constructor() {
    this.lastBounds = { x: -1, y: -1, width: -1, height: -1 };
    this.rafId = null;
    this.isSyncing = false;

    this.init();
  }

  init() {
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', () => this.bindObservers());
    } else {
      this.bindObservers();
    }
  }

  bindObservers() {
    const target = document.getElementById('viewport') || document.getElementById('center-container');
    if (!target) {
      setTimeout(() => this.bindObservers(), 100);
      return;
    }

    // ResizeObserver watches panel docks and splitters
    const observer = new ResizeObserver(() => {
      this.scheduleSync();
    });
    observer.observe(target);

    // Also observe outer window resize
    window.addEventListener('resize', () => {
      this.scheduleSync();
    });

    // Listen to IPC responses
    window.addEventListener('backend-message', (evt) => {
      const data = evt.detail;
      if (data && data.type === 'sync_viewport_bounds_response') {
        if (!data.success && data.error) {
          console.warn('[ViewportSync] Backend warning:', data.error);
        }
      }
    });

    // Initial sync
    this.scheduleSync();
  }

  scheduleSync() {
    if (this.rafId) cancelAnimationFrame(this.rafId);
    this.rafId = requestAnimationFrame(() => this.performSync());
  }

  performSync() {
    const target = document.getElementById('viewport') || document.getElementById('center-container');
    if (!target) return;

    const rect = target.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1.0;

    const x = Math.round(rect.left * dpr);
    const y = Math.round(rect.top * dpr);
    const width = Math.round(rect.width * dpr);
    const height = Math.round(rect.height * dpr);

    // Only dispatch if dimensions actually changed
    if (
      x === this.lastBounds.x &&
      y === this.lastBounds.y &&
      width === this.lastBounds.width &&
      height === this.lastBounds.height
    ) {
      return;
    }

    this.lastBounds = { x, y, width, height };

    if (width <= 0 || height <= 0) return;

    const payload = {
      action: 'sync_viewport_bounds',
      x,
      y,
      width,
      height,
      devicePixelRatio: dpr
    };

    if (window.external && typeof window.external.sendMessage === 'function') {
      try {
        window.external.sendMessage(JSON.stringify(payload));
      } catch (err) {
        console.error('[ViewportSync] Failed to send bounds to native backend:', err);
      }
    }
  }
}

export const viewportSync = new ViewportSynchronizer();
window.viewportSync = viewportSync;

