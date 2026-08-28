import { canonicalizeJcs } from './canonicalize';
import type { JsonObject } from './types';

export type LedgerHashInput = {
  type: string;
  payload: JsonObject;
  createdAt: string;
  prevHash: string;
};

export function createLedgerHashDocument(input: LedgerHashInput): JsonObject {
  return {
    createdAt: input.createdAt,
    payload: input.payload,
    prevHash: input.prevHash,
    type: input.type,
  };
}

export async function sha256Hex(text: string): Promise<string> {
  const subtle = globalThis.crypto?.subtle;
  if (!subtle) {
    throw new Error('WebCrypto SubtleCrypto is unavailable');
  }

  const encoded = new TextEncoder().encode(text);
  const data = encoded.buffer.slice(encoded.byteOffset, encoded.byteOffset + encoded.byteLength) as ArrayBuffer;
  const digest = await subtle.digest('SHA-256', data);
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('');
}

export async function computeLedgerEntryHash(input: LedgerHashInput): Promise<string> {
  return sha256Hex(canonicalizeJcs(createLedgerHashDocument(input)));
}
