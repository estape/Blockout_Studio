/**
 * @file blueprintNodes.js
 * @brief Node catalog, pin type definitions, and node factory for UE5-inspired visual scripting.
 */

export const PinTypes = {
  EXEC: 'Exec',
  BOOLEAN: 'Boolean',
  INTEGER: 'Integer',
  FLOAT: 'Float',
  VECTOR3: 'Vector3',
  STRING: 'String',
  ACTOR_REF: 'ActorRef'
};

export const PinColors = {
  Exec: '#ffffff',
  Boolean: '#dc2626',
  Integer: '#06b6d4',
  Float: '#22c55e',
  Vector3: '#f59e0b',
  String: '#ec4899',
  ActorRef: '#3b82f6'
};

export const NodeCategories = {
  EVENT: 'Event',
  ACTOR: 'Actor',
  MATH: 'Math',
  FLOW_CONTROL: 'Flow Control',
  FLOWCONTROL: 'Flow Control',
  VARIABLE: 'Variable'
};

/**
 * Registry of available Blueprint Node definitions.
 */
export const NodeRegistry = {
  // --- EVENTS ---
  EventBeginPlay: {
    type: 'EventBeginPlay',
    title: 'Event BeginPlay',
    category: NodeCategories.EVENT,
    inputs: [],
    outputs: [
      { name: 'Exec', type: PinTypes.EXEC }
    ]
  },
  EventTick: {
    type: 'EventTick',
    title: 'Event Tick',
    category: NodeCategories.EVENT,
    inputs: [],
    outputs: [
      { name: 'Exec', type: PinTypes.EXEC },
      { name: 'DeltaSeconds', type: PinTypes.FLOAT }
    ]
  },
  OnCollisionEnter: {
    type: 'OnCollisionEnter',
    title: 'On Collision Enter',
    category: NodeCategories.EVENT,
    inputs: [],
    outputs: [
      { name: 'Exec', type: PinTypes.EXEC },
      { name: 'Other', type: PinTypes.ACTOR_REF }
    ]
  },
  OnTriggerEnter: {
    type: 'OnTriggerEnter',
    title: 'On Trigger Enter',
    category: NodeCategories.EVENT,
    inputs: [],
    outputs: [
      { name: 'Exec', type: PinTypes.EXEC },
      { name: 'Other', type: PinTypes.ACTOR_REF }
    ]
  },
  CustomEvent: {
    type: 'CustomEvent',
    title: 'Custom Event',
    category: NodeCategories.EVENT,
    properties: { EventName: 'CustomEvent' },
    inputs: [],
    outputs: [
      { name: 'Exec', type: PinTypes.EXEC }
    ]
  },

  // --- FLOW CONTROL ---
  Branch: {
    type: 'Branch',
    title: 'Branch',
    displayName: 'Branch',
    methodName: 'Branch',
    category: NodeCategories.FLOW_CONTROL,
    inputs: [
      { name: 'ExecIn', type: PinTypes.EXEC },
      { name: 'Condition', type: PinTypes.BOOLEAN, defaultValue: 'false' }
    ],
    outputs: [
      { name: 'True', type: PinTypes.EXEC },
      { name: 'False', type: PinTypes.EXEC }
    ]
  },
  Sequence: {
    type: 'Sequence',
    title: 'Sequence',
    displayName: 'Sequence',
    methodName: 'Sequence',
    category: NodeCategories.FLOW_CONTROL,
    inputs: [
      { name: 'ExecIn', type: PinTypes.EXEC }
    ],
    outputs: [
      { name: 'Then 0', type: PinTypes.EXEC },
      { name: 'Then 1', type: PinTypes.EXEC }
    ]
  },
  ForEachLoop: {
    type: 'ForEachLoop',
    title: 'For Each Loop',
    displayName: 'For Each Loop',
    methodName: 'ForEachLoop',
    category: NodeCategories.FLOW_CONTROL,
    inputs: [
      { name: 'ExecIn', type: PinTypes.EXEC },
      { name: 'Array', type: PinTypes.ACTOR_REF }
    ],
    outputs: [
      { name: 'LoopBody', type: PinTypes.EXEC },
      { name: 'ArrayElement', type: PinTypes.ACTOR_REF },
      { name: 'ArrayIndex', type: PinTypes.INTEGER },
      { name: 'Completed', type: PinTypes.EXEC }
    ]
  },
  Delay: {
    type: 'Delay',
    title: 'Delay',
    displayName: 'Delay',
    methodName: 'Delay',
    category: NodeCategories.FLOW_CONTROL,
    inputs: [
      { name: 'ExecIn', type: PinTypes.EXEC },
      { name: 'Duration', type: PinTypes.FLOAT, defaultValue: '0.2' }
    ],
    outputs: [
      { name: 'Completed', type: PinTypes.EXEC }
    ]
  },
  DoOnce: {
    type: 'DoOnce',
    title: 'Do Once',
    displayName: 'Do Once',
    methodName: 'DoOnce',
    category: NodeCategories.FLOW_CONTROL,
    inputs: [
      { name: 'ExecIn', type: PinTypes.EXEC },
      { name: 'Reset', type: PinTypes.EXEC },
      { name: 'StartClosed', type: PinTypes.BOOLEAN, defaultValue: 'false' }
    ],
    outputs: [
      { name: 'Completed', type: PinTypes.EXEC }
    ]
  },
  ForLoop: {
    type: 'ForLoop',
    title: 'For Loop',
    displayName: 'For Loop',
    methodName: 'ForLoop',
    category: NodeCategories.FLOW_CONTROL,
    inputs: [
      { name: 'ExecIn', type: PinTypes.EXEC },
      { name: 'FirstIndex', type: PinTypes.INTEGER, defaultValue: '0' },
      { name: 'LastIndex', type: PinTypes.INTEGER, defaultValue: '10' }
    ],
    outputs: [
      { name: 'LoopBody', type: PinTypes.EXEC },
      { name: 'Index', type: PinTypes.INTEGER },
      { name: 'Completed', type: PinTypes.EXEC }
    ]
  },

  // --- ACTOR OPERATIONS ---
  GetActorLocation: {
    type: 'GetActorLocation',
    title: 'Get Actor Location',
    category: NodeCategories.ACTOR,
    inputs: [],
    outputs: [
      { name: 'Location', type: PinTypes.VECTOR3 },
      { name: 'X', type: PinTypes.FLOAT },
      { name: 'Y', type: PinTypes.FLOAT },
      { name: 'Z', type: PinTypes.FLOAT }
    ]
  },
  SetActorLocation: {
    type: 'SetActorLocation',
    title: 'Set Actor Location',
    category: NodeCategories.ACTOR,
    inputs: [
      { name: 'Exec', type: PinTypes.EXEC },
      { name: 'NewLocation', type: PinTypes.VECTOR3, defaultValue: '0,0,0' }
    ],
    outputs: [
      { name: 'Exec', type: PinTypes.EXEC }
    ]
  },
  GetActorRotation: {
    type: 'GetActorRotation',
    title: 'Get Actor Rotation',
    category: NodeCategories.ACTOR,
    inputs: [],
    outputs: [
      { name: 'Rotation', type: PinTypes.VECTOR3 },
      { name: 'Pitch', type: PinTypes.FLOAT },
      { name: 'Yaw', type: PinTypes.FLOAT },
      { name: 'Roll', type: PinTypes.FLOAT }
    ]
  },
  SetActorRotation: {
    type: 'SetActorRotation',
    title: 'Set Actor Rotation',
    category: NodeCategories.ACTOR,
    inputs: [
      { name: 'Exec', type: PinTypes.EXEC },
      { name: 'NewRotation', type: PinTypes.VECTOR3, defaultValue: '0,0,0' }
    ],
    outputs: [
      { name: 'Exec', type: PinTypes.EXEC }
    ]
  },
  AddActorWorldOffset: {
    type: 'AddActorWorldOffset',
    title: 'Add Actor World Offset',
    category: NodeCategories.ACTOR,
    inputs: [
      { name: 'Exec', type: PinTypes.EXEC },
      { name: 'DeltaLocation', type: PinTypes.VECTOR3, defaultValue: '0,0,0' }
    ],
    outputs: [
      { name: 'Exec', type: PinTypes.EXEC }
    ]
  },
  AddActorWorldRotation: {
    type: 'AddActorWorldRotation',
    title: 'Add Actor World Rotation',
    category: NodeCategories.ACTOR,
    inputs: [
      { name: 'Exec', type: PinTypes.EXEC },
      { name: 'DeltaRotation', type: PinTypes.VECTOR3, defaultValue: '0,0,0' }
    ],
    outputs: [
      { name: 'Exec', type: PinTypes.EXEC }
    ]
  },
  PlaySound2D: {
    type: 'PlaySound2D',
    title: 'Play Sound 2D',
    category: NodeCategories.ACTOR,
    inputs: [
      { name: 'Exec', type: PinTypes.EXEC },
      { name: 'Sound', type: PinTypes.STRING, defaultValue: 'footstep' },
      { name: 'Volume', type: PinTypes.FLOAT, defaultValue: '1.0' }
    ],
    outputs: [
      { name: 'Exec', type: PinTypes.EXEC }
    ]
  },
  PrintString: {
    type: 'PrintString',
    title: 'Print String',
    category: NodeCategories.ACTOR,
    inputs: [
      { name: 'Exec', type: PinTypes.EXEC },
      { name: 'InString', type: PinTypes.STRING, defaultValue: 'Hello from Blueprint!' }
    ],
    outputs: [
      { name: 'Exec', type: PinTypes.EXEC }
    ]
  },

  // --- MATH & LOGIC ---
  MakeVector: {
    type: 'MakeVector',
    title: 'Make Vector',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'X', type: PinTypes.FLOAT, defaultValue: '0' },
      { name: 'Y', type: PinTypes.FLOAT, defaultValue: '0' },
      { name: 'Z', type: PinTypes.FLOAT, defaultValue: '0' }
    ],
    outputs: [
      { name: 'Return Value', type: PinTypes.VECTOR3 }
    ]
  },
  BreakVector: {
    type: 'BreakVector',
    title: 'Break Vector',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'In', type: PinTypes.VECTOR3 }
    ],
    outputs: [
      { name: 'X', type: PinTypes.FLOAT },
      { name: 'Y', type: PinTypes.FLOAT },
      { name: 'Z', type: PinTypes.FLOAT }
    ]
  },
  Add: {
    type: 'Add',
    title: 'Add (+)',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'A', type: PinTypes.FLOAT, defaultValue: '0' },
      { name: 'B', type: PinTypes.FLOAT, defaultValue: '0' }
    ],
    outputs: [
      { name: 'Result', type: PinTypes.FLOAT }
    ]
  },
  Subtract: {
    type: 'Subtract',
    title: 'Subtract (-)',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'A', type: PinTypes.FLOAT, defaultValue: '0' },
      { name: 'B', type: PinTypes.FLOAT, defaultValue: '0' }
    ],
    outputs: [
      { name: 'Result', type: PinTypes.FLOAT }
    ]
  },
  Multiply: {
    type: 'Multiply',
    title: 'Multiply (*)',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'A', type: PinTypes.FLOAT, defaultValue: '1' },
      { name: 'B', type: PinTypes.FLOAT, defaultValue: '1' }
    ],
    outputs: [
      { name: 'Result', type: PinTypes.FLOAT }
    ]
  },
  Divide: {
    type: 'Divide',
    title: 'Divide (/)',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'A', type: PinTypes.FLOAT, defaultValue: '1' },
      { name: 'B', type: PinTypes.FLOAT, defaultValue: '1' }
    ],
    outputs: [
      { name: 'Result', type: PinTypes.FLOAT }
    ]
  },
  Lerp: {
    type: 'Lerp',
    title: 'Lerp (Linear Interpolate)',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'A', type: PinTypes.FLOAT, defaultValue: '0' },
      { name: 'B', type: PinTypes.FLOAT, defaultValue: '100' },
      { name: 'Alpha', type: PinTypes.FLOAT, defaultValue: '0.5' }
    ],
    outputs: [
      { name: 'Result', type: PinTypes.FLOAT }
    ]
  },
  VectorDistance: {
    type: 'VectorDistance',
    title: 'Vector Distance',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'V1', type: PinTypes.VECTOR3 },
      { name: 'V2', type: PinTypes.VECTOR3 }
    ],
    outputs: [
      { name: 'Distance', type: PinTypes.FLOAT }
    ]
  },
  CompareEqual: {
    type: 'CompareEqual',
    title: 'Equal (==)',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'A', type: PinTypes.FLOAT, defaultValue: '0' },
      { name: 'B', type: PinTypes.FLOAT, defaultValue: '0' }
    ],
    outputs: [
      { name: 'Equal', type: PinTypes.BOOLEAN }
    ]
  },
  CompareNotEqual: {
    type: 'CompareNotEqual',
    title: 'Not Equal (!=)',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'A', type: PinTypes.FLOAT, defaultValue: '0' },
      { name: 'B', type: PinTypes.FLOAT, defaultValue: '0' }
    ],
    outputs: [
      { name: 'Not Equal', type: PinTypes.BOOLEAN }
    ]
  },
  CompareGreater: {
    type: 'CompareGreater',
    title: 'Greater (>)',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'A', type: PinTypes.FLOAT, defaultValue: '0' },
      { name: 'B', type: PinTypes.FLOAT, defaultValue: '0' }
    ],
    outputs: [
      { name: 'Greater', type: PinTypes.BOOLEAN }
    ]
  },
  CompareLess: {
    type: 'CompareLess',
    title: 'Less (<)',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'A', type: PinTypes.FLOAT, defaultValue: '0' },
      { name: 'B', type: PinTypes.FLOAT, defaultValue: '0' }
    ],
    outputs: [
      { name: 'Less', type: PinTypes.BOOLEAN }
    ]
  },
  LogicalAnd: {
    type: 'LogicalAnd',
    title: 'AND (&&)',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'A', type: PinTypes.BOOLEAN, defaultValue: 'true' },
      { name: 'B', type: PinTypes.BOOLEAN, defaultValue: 'true' }
    ],
    outputs: [
      { name: 'Result', type: PinTypes.BOOLEAN }
    ]
  },
  LogicalOr: {
    type: 'LogicalOr',
    title: 'OR (||)',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'A', type: PinTypes.BOOLEAN, defaultValue: 'false' },
      { name: 'B', type: PinTypes.BOOLEAN, defaultValue: 'false' }
    ],
    outputs: [
      { name: 'Result', type: PinTypes.BOOLEAN }
    ]
  },
  LogicalNot: {
    type: 'LogicalNot',
    title: 'NOT (!)',
    category: NodeCategories.MATH,
    inputs: [
      { name: 'A', type: PinTypes.BOOLEAN, defaultValue: 'false' }
    ],
    outputs: [
      { name: 'Result', type: PinTypes.BOOLEAN }
    ]
  },

  // --- VARIABLES ---
  GetVariable: {
    type: 'GetVariable',
    title: 'Get Variable',
    category: NodeCategories.VARIABLE,
    properties: { VariableName: 'NewVariable' },
    inputs: [],
    outputs: [
      { name: 'Value', type: PinTypes.FLOAT }
    ]
  },
  SetVariable: {
    type: 'SetVariable',
    title: 'Set Variable',
    category: NodeCategories.VARIABLE,
    properties: { VariableName: 'NewVariable' },
    inputs: [
      { name: 'Exec', type: PinTypes.EXEC },
      { name: 'Value', type: PinTypes.FLOAT, defaultValue: '0' }
    ],
    outputs: [
      { name: 'Exec', type: PinTypes.EXEC }
    ]
  }
};

/**
 * Creates an instance of a node from registry type.
 * @param {string} nodeType
 * @param {number} posX
 * @param {number} posY
 * @param {Object} [customProps={}]
 * @returns {Object} Node instance
 */
export function createNodeInstance(nodeType, posX = 0, posY = 0, customProps = {}) {
  let def = NodeRegistry[nodeType];
  if (!def && typeof window !== 'undefined' && window.nodeCatalog && Array.isArray(window.nodeCatalog)) {
    const found = window.nodeCatalog.find(n => n.id === nodeType || n.nodeType === nodeType || n.methodName === nodeType || n.title === nodeType);
    if (found) {
      registerDynamicNodes([found]);
      def = NodeRegistry[nodeType] || NodeRegistry[found.id];
    }
  }
  if (!def) {
    def = NodeRegistry['EventBeginPlay'];
  }
  const nodeId = 'node_' + Math.random().toString(36).substring(2, 9);

  const inputs = (def.inputs || []).map(p => ({
    id: `pin_${Math.random().toString(36).substring(2, 9)}`,
    name: p.name,
    type: p.type,
    direction: 'Input',
    defaultValue: p.defaultValue || null
  }));

  const outputs = (def.outputs || []).map(p => ({
    id: `pin_${Math.random().toString(36).substring(2, 9)}`,
    name: p.name,
    type: p.type,
    direction: 'Output'
  }));

  let title = def.title;
  const props = { ...(def.properties || {}), ...customProps };
  if (nodeType === 'GetVariable' && props.VariableName) {
    title = `Get ${props.VariableName}`;
  } else if (nodeType === 'SetVariable' && props.VariableName) {
    title = `Set ${props.VariableName}`;
  }

  return {
    id: nodeId,
    nodeType: def.type,
    title,
    category: def.category,
    posX,
    posY,
    inputs,
    outputs,
    properties: props
  };
}

/**
 * Determines whether two pins are type-compatible for connection.
 * @param {Object} fromPin Output pin
 * @param {Object} toPin Input pin
 * @returns {boolean}
 */
export function arePinsCompatible(fromPin, toPin) {
  if (!fromPin || !toPin) return false;
  if (fromPin.direction === toPin.direction) return false;

  // Exec can only connect to Exec
  if (fromPin.type === PinTypes.EXEC || toPin.type === PinTypes.EXEC) {
    return fromPin.type === PinTypes.EXEC && toPin.type === PinTypes.EXEC;
  }

  // Exact type match
  if (fromPin.type === toPin.type) return true;

  // Auto-casting: Int -> Float
  if (fromPin.type === PinTypes.INTEGER && toPin.type === PinTypes.FLOAT) return true;

  // Anything -> String
  if (toPin.type === PinTypes.STRING) return true;

  return false;
}

/**
 * Dynamically registers or updates node definitions discovered via C# reflection.
 * @param {Array<Object>} discoveredNodes List of node definitions from C# NodeCatalog.
 */
export function registerDynamicNodes(discoveredNodes) {
  if (!Array.isArray(discoveredNodes)) return;

  for (const node of discoveredNodes) {
    if (!node || !node.id) continue;

    const category = node.category || 'Actor';
    if (!NodeCategories[category.toUpperCase()]) {
      NodeCategories[category.toUpperCase()] = category;
    }

    const inputs = (node.inputs || []).map(p => ({
      name: p.name,
      type: p.type || PinTypes.EXEC,
      defaultValue: p.defaultValue ?? null
    }));

    const outputs = (node.outputs || []).map(p => ({
      name: p.name,
      type: p.type || PinTypes.EXEC,
      defaultValue: p.defaultValue ?? null
    }));

    NodeRegistry[node.id] = {
      type: node.id,
      title: node.title || node.nodeType,
      displayName: node.title || node.nodeType,
      category: category,
      isPure: node.isPure || false,
      declaringType: node.declaringType,
      methodName: node.methodName || node.nodeType,
      isStatic: node.isStatic,
      inputs,
      outputs,
      properties: node.properties || {}
    };

    // Also register under nodeType and methodName if not already claimed
    if (!NodeRegistry[node.nodeType]) {
      NodeRegistry[node.nodeType] = NodeRegistry[node.id];
    }
    if (node.methodName && !NodeRegistry[node.methodName]) {
      NodeRegistry[node.methodName] = NodeRegistry[node.id];
    }
  }
}
