/**
 * @file blueprintRenderer.js
 * @brief SVG Bezier wire rendering, pin coordinate tracking, and visual connection pulses.
 */

import { PinColors, PinTypes } from './blueprintNodes.js';

export class BlueprintRenderer {
  /**
   * @param {SVGSVGElement} svgLayer
   */
  constructor(svgLayer) {
    this.svg = svgLayer;
    this.selectedConnectionId = null;
    this.onConnectionDeleted = null;
    this.tempWirePath = null;
  }

  /**
   * Calculates a smooth horizontal cubic bezier curve between two points.
   * @param {number} x1
   * @param {number} y1
   * @param {number} x2
   * @param {number} y2
   * @returns {string} SVG Path 'd' attribute string
   */
  static getCubicBezierPath(x1, y1, x2, y2) {
    const dx = Math.max(Math.abs(x2 - x1) * 0.5, 40);
    const cx1 = x1 + dx;
    const cy1 = y1;
    const cx2 = x2 - dx;
    const cy2 = y2;
    return `M ${x1.toFixed(1)} ${y1.toFixed(1)} C ${cx1.toFixed(1)} ${cy1.toFixed(1)}, ${cx2.toFixed(1)} ${cy2.toFixed(1)}, ${x2.toFixed(1)} ${y2.toFixed(1)}`;
  }

  /**
   * Redraws all wires in the graph.
   * @param {Array<Object>} connections
   * @param {Map<string, {x: number, y: number, pin: Object}>} pinCoordsMap
   * @param {boolean} isPlaying
   */
  renderConnections(connections, pinCoordsMap, isPlaying = false) {
    // Retain temp wire if currently dragging
    const tempD = this.tempWirePath ? this.tempWirePath.getAttribute('d') : null;
    const tempStroke = this.tempWirePath ? this.tempWirePath.getAttribute('stroke') : null;

    this.svg.innerHTML = '';

    for (const conn of connections) {
      const fromKey = `${conn.fromNodeId}_${conn.fromPinId}`;
      const toKey = `${conn.toNodeId}_${conn.toPinId}`;

      const fromCoord = pinCoordsMap.get(fromKey);
      const toCoord = pinCoordsMap.get(toKey);

      if (!fromCoord || !toCoord) continue;

      const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
      path.setAttribute('d', BlueprintRenderer.getCubicBezierPath(fromCoord.x, fromCoord.y, toCoord.x, toCoord.y));
      path.setAttribute('class', `bp-wire ${fromCoord.pin.type === PinTypes.EXEC ? 'exec' : ''} ${isPlaying && fromCoord.pin.type === PinTypes.EXEC ? 'pulse' : ''}`);

      const strokeColor = PinColors[fromCoord.pin.type] || '#ffffff';
      path.setAttribute('stroke', strokeColor);

      if (this.selectedConnectionId === conn.id) {
        path.setAttribute('stroke', '#f59e0b');
        path.setAttribute('stroke-width', '4');
      }

      path.dataset.connectionId = conn.id;

      path.addEventListener('click', (e) => {
        e.stopPropagation();
        this.selectedConnectionId = conn.id;
        this.renderConnections(connections, pinCoordsMap, isPlaying);
      });

      this.svg.appendChild(path);
    }

    if (tempD && tempStroke) {
      this.drawTempWireRaw(tempD, tempStroke);
    }
  }

  /**
   * Draws interactive in-progress wire from a source pin to mouse cursor.
   * @param {number} x1
   * @param {number} y1
   * @param {number} x2
   * @param {number} y2
   * @param {string} pinType
   */
  drawTempWire(x1, y1, x2, y2, pinType) {
    const stroke = PinColors[pinType] || '#ffffff';
    const d = BlueprintRenderer.getCubicBezierPath(x1, y1, x2, y2);
    this.drawTempWireRaw(d, stroke);
  }

  drawTempWireRaw(d, stroke) {
    if (!this.tempWirePath) {
      this.tempWirePath = document.createElementNS('http://www.w3.org/2000/svg', 'path');
      this.tempWirePath.setAttribute('class', 'bp-wire');
      this.tempWirePath.setAttribute('stroke-dasharray', '4 4');
      this.svg.appendChild(this.tempWirePath);
    }
    this.tempWirePath.setAttribute('d', d);
    this.tempWirePath.setAttribute('stroke', stroke);
  }

  clearTempWire() {
    if (this.tempWirePath && this.tempWirePath.parentNode) {
      this.tempWirePath.parentNode.removeChild(this.tempWirePath);
    }
    this.tempWirePath = null;
  }
}
