/**
 * @file ipcBridge.js
 * @brief Photino.NET C# backend IPC communication bridge and UI toast notification system.
 */

// ============================================================================
// PHOTINO.NET C# BACKEND IPC BRIDGE & TOAST SYSTEM
// ============================================================================

/**
 * @brief Sends a serialized JSON action payload to the Photino.NET C# backend host.
 * @param {string} action The action verb identifying the backend handler.
 * @param {Object} [payload={}] Additional payload parameters to send.
 */
export function sendToBackend(action, payload = {}) {
  const json = JSON.stringify({ action, ...payload });
  if (window.external && window.external.sendMessage) {
    window.external.sendMessage(json);
  } else {
    console.log('[Browser Fallback] IPC Message:', action, payload);
  }
}

/**
 * @type {Function}
 * @brief Alias for sendToBackend.
 */
export const sendIpcMessage = sendToBackend;

if (typeof window !== 'undefined') {
  window.sendToBackend = sendToBackend;
  window.sendIpcMessage = sendToBackend;
}

/**
 * @brief Displays a floating toast notification in the application interface.
 * @param {string} message The text content to display in the toast banner.
 * @param {boolean} [isError=false] Indicates whether the toast indicates an error state.
 */
export function showToast(message, isError = false) {
  const toast = document.getElementById('toast-msg');
  if (!toast) return;
  toast.textContent = message;
  toast.style.background = isError ? 'rgba(224, 72, 72, 0.95)' : 'rgba(30, 130, 76, 0.95)';
  toast.style.display = 'block';
  setTimeout(() => { toast.style.display = 'none'; }, 3500);
}

/**
 * @type {Map<string, Function>}
 * @brief Map of registered IPC message type handlers.
 */
const messageHandlers = new Map();

/**
 * @brief Registers an asynchronous response handler for messages matching a specific type.
 * @param {string} type The message type identifier returned by the backend.
 * @param {Function} handler Callback receiving the parsed message payload.
 */
export function registerHandler(type, handler) {
  messageHandlers.set(type, handler);
}

if (typeof window !== 'undefined' && window.external && window.external.receiveMessage) {
  window.external.receiveMessage((raw) => {
    try {
      const msg = JSON.parse(raw);
      // Dispatch DOM CustomEvent for modules using window.addEventListener('ipc-message')
      window.dispatchEvent(new CustomEvent('ipc-message', { detail: msg }));

      if (msg.type && messageHandlers.has(msg.type)) {
        messageHandlers.get(msg.type)(msg);
      } else {
        console.log('Unhandled backend message:', msg);
      }
    } catch (e) {
      console.error('Failed to parse backend message:', e, raw);
    }
  });
}
