// ============================================================================
// PHOTINO.NET C# BACKEND IPC BRIDGE & TOAST SYSTEM
// ============================================================================

export function sendToBackend(action, payload = {}) {
  const json = JSON.stringify({ action, ...payload });
  if (window.external && window.external.sendMessage) {
    window.external.sendMessage(json);
  } else {
    console.log('[Browser Fallback] IPC Message:', action, payload);
  }
}

export function showToast(message, isError = false) {
  const toast = document.getElementById('toast-msg');
  if (!toast) return;
  toast.textContent = message;
  toast.style.background = isError ? 'rgba(224, 72, 72, 0.95)' : 'rgba(30, 130, 76, 0.95)';
  toast.style.display = 'block';
  setTimeout(() => { toast.style.display = 'none'; }, 3500);
}

const messageHandlers = new Map();

export function registerHandler(type, handler) {
  messageHandlers.set(type, handler);
}

if (window.external && window.external.receiveMessage) {
  window.external.receiveMessage((raw) => {
    try {
      const msg = JSON.parse(raw);
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

