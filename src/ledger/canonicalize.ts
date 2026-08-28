function assertValidUnicode(value: string, label: string): void {
  for (let index = 0; index < value.length; index += 1) {
    const code = value.charCodeAt(index);
    if (code >= 0xd800 && code <= 0xdbff) {
      const next = value.charCodeAt(index + 1);
      if (!(next >= 0xdc00 && next <= 0xdfff)) {
        throw new TypeError(`${label} contains an unpaired high surrogate`);
      }
      index += 1;
      continue;
    }
    if (code >= 0xdc00 && code <= 0xdfff) {
      throw new TypeError(`${label} contains an unpaired low surrogate`);
    }
  }
}

function isPlainObject(value: object): value is Record<string, unknown> {
  const prototype = Object.getPrototypeOf(value);
  return prototype === Object.prototype || prototype === null;
}

function serialize(value: unknown, stack: WeakSet<object>): string {
  if (value === null) {
    return 'null';
  }

  switch (typeof value) {
    case 'string': {
      assertValidUnicode(value, 'JSON string');
      return JSON.stringify(value);
    }
    case 'number': {
      if (!Number.isFinite(value)) {
        throw new TypeError('JCS does not allow NaN or Infinity');
      }
      return JSON.stringify(value);
    }
    case 'boolean':
      return value ? 'true' : 'false';
    case 'object':
      break;
    default:
      throw new TypeError(`Unsupported JSON value type: ${typeof value}`);
  }

  if (stack.has(value)) {
    throw new TypeError('JCS does not allow cyclic values');
  }
  stack.add(value);

  try {
    if (Array.isArray(value)) {
      const items: string[] = [];
      for (let index = 0; index < value.length; index += 1) {
        if (!(index in value)) {
          throw new TypeError('JCS does not allow sparse arrays');
        }
        items.push(serialize(value[index], stack));
      }
      return `[${items.join(',')}]`;
    }

    if (!isPlainObject(value)) {
      throw new TypeError('JCS input must contain only plain JSON objects');
    }
    if (Object.getOwnPropertySymbols(value).length > 0) {
      throw new TypeError('JCS does not allow symbol properties');
    }

    const keys = Object.keys(value).sort();
    const members: string[] = [];
    for (const key of keys) {
      assertValidUnicode(key, 'JSON object key');
      members.push(`${JSON.stringify(key)}:${serialize(value[key], stack)}`);
    }
    return `{${members.join(',')}}`;
  } finally {
    stack.delete(value);
  }
}

/** RFC 8785 / JCS canonical JSON for the JSON data model used by the ledger. */
export function canonicalizeJcs(value: unknown): string {
  return serialize(value, new WeakSet<object>());
}
