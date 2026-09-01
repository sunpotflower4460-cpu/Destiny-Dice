import { LEDGER_ENTRY_TYPES, type LedgerEntryType } from '../db/schema';
import { summarizeBitstream } from '../stats/core';
import { canonicalizeJcs } from './canonicalize';
import { computeLedgerEntryHash } from './hash';
import { GENESIS_PREV_HASH, type JsonObject, type StoredLedgerEntry } from './types';

const HASH_PATTERN = /^[0-9a-f]{64}$/;

export type VerifyChainErrorCode =
  | 'empty_chain'
  | 'invalid_seq'
  | 'invalid_type'
  | 'invalid_genesis_type'
  | 'invalid_genesis_prev_hash'
  | 'invalid_prev_hash'
  | 'invalid_entry_hash_format'
  | 'invalid_payload_json'
  | 'non_canonical_payload'
  | 'entry_hash_mismatch'
  | 'invalid_prediction_binding'
  | 'invalid_recorded_stats';

export type VerifyChainResult =
  | { ok: true; entries: number; headHash: string }
  | {
      ok: false;
      code: VerifyChainErrorCode;
      index: number;
      seq?: number;
      message: string;
    };

function fail(
  code: VerifyChainErrorCode,
  index: number,
  message: string,
  seq?: number,
): VerifyChainResult {
  return seq === undefined ? { ok: false, code, index, message } : { ok: false, code, index, seq, message };
}

function isLedgerEntryType(value: string): value is LedgerEntryType {
  return (LEDGER_ENTRY_TYPES as readonly string[]).includes(value);
}

function parsePayload(payloadJson: string): JsonObject | null {
  let parsed: unknown;
  try {
    parsed = JSON.parse(payloadJson);
  } catch {
    return null;
  }
  if (parsed === null || Array.isArray(parsed) || typeof parsed !== 'object') {
    return null;
  }
  return parsed as JsonObject;
}

function identityField(payload: JsonObject, key: 'date' | 'seqInDay' | 'condition' | 'targetDir'): string | number | null {
  const value = payload[key];
  if (key === 'date') return typeof value === 'string' ? value : null;
  return typeof value === 'number' && Number.isSafeInteger(value) ? value : null;
}

function sessionMatchesPredictionIdentity(session: JsonObject, prediction: JsonObject): boolean {
  const date = identityField(session, 'date');
  const seqInDay = identityField(session, 'seqInDay');
  const condition = identityField(session, 'condition');
  const targetDir = identityField(session, 'targetDir');
  if (date === null || seqInDay === null || condition === null || targetDir === null) return false;
  return (
    identityField(prediction, 'date') === date &&
    identityField(prediction, 'seqInDay') === seqInDay &&
    identityField(prediction, 'condition') === condition &&
    identityField(prediction, 'targetDir') === targetDir
  );
}

function recordedBitstreamMatches(payload: JsonObject, targetDir: 0 | 1): boolean | 'skip' {
  if (
    typeof payload.bitsHex !== 'string' ||
    typeof payload.nBits !== 'number' ||
    typeof payload.hits !== 'number' ||
    typeof payload.z !== 'number'
  ) {
    return 'skip';
  }
  try {
    const frozen = summarizeBitstream(payload.bitsHex, payload.nBits, targetDir);
    return frozen.hits === payload.hits && Math.abs(frozen.z - payload.z) <= 1e-12;
  } catch {
    return false;
  }
}

export async function verifyChain(entries: readonly StoredLedgerEntry[]): Promise<VerifyChainResult> {
  if (entries.length === 0) {
    return fail('empty_chain', 0, 'Ledger chain is empty');
  }

  let previousHash = GENESIS_PREV_HASH;

  for (let index = 0; index < entries.length; index += 1) {
    const entry = entries[index]!;
    const expectedSeq = index + 1;

    if (!Number.isSafeInteger(entry.seq) || entry.seq !== expectedSeq) {
      return fail('invalid_seq', index, `Expected seq ${expectedSeq}, received ${String(entry.seq)}`, entry.seq);
    }
    if (!isLedgerEntryType(entry.type)) {
      return fail('invalid_type', index, `Unsupported ledger type: ${String(entry.type)}`, entry.seq);
    }
    if (index === 0 && entry.type !== 'registration') {
      return fail('invalid_genesis_type', index, 'First ledger entry must be registration', entry.seq);
    }
    if (index === 0 && entry.prevHash !== GENESIS_PREV_HASH) {
      return fail('invalid_genesis_prev_hash', index, 'Genesis prevHash must be 64 zeroes', entry.seq);
    }
    if (index > 0 && entry.prevHash !== previousHash) {
      return fail('invalid_prev_hash', index, 'prevHash does not match the previous entry hash', entry.seq);
    }
    if (!HASH_PATTERN.test(entry.entryHash)) {
      return fail('invalid_entry_hash_format', index, 'entryHash must be 64 lowercase hexadecimal characters', entry.seq);
    }

    const payload = parsePayload(entry.payloadJson);
    if (!payload) {
      return fail('invalid_payload_json', index, 'payloadJson is not a JSON object', entry.seq);
    }

    let canonicalPayload: string;
    try {
      canonicalPayload = canonicalizeJcs(payload);
    } catch (error) {
      return fail(
        'invalid_payload_json',
        index,
        error instanceof Error ? error.message : String(error),
        entry.seq,
      );
    }
    if (canonicalPayload !== entry.payloadJson) {
      return fail('non_canonical_payload', index, 'Stored payloadJson is not RFC 8785/JCS canonical', entry.seq);
    }

    const expectedHash = await computeLedgerEntryHash({
      type: entry.type,
      payload,
      createdAt: entry.createdAt,
      prevHash: entry.prevHash,
    });
    if (expectedHash !== entry.entryHash) {
      return fail('entry_hash_mismatch', index, 'entryHash does not match canonical entry contents', entry.seq);
    }

    if (entry.type === 'control') {
      const stats = recordedBitstreamMatches(payload, 1);
      if (stats === false) {
        return fail(
          'invalid_recorded_stats',
          index,
          'control bits/hits/z do not match the frozen stats core',
          entry.seq,
        );
      }
    }

    if (entry.type === 'session') {
      const predictionSeq = payload.predictionSeq;
      if (!Number.isSafeInteger(predictionSeq) || (predictionSeq as number) < 1 || (predictionSeq as number) >= entry.seq) {
        return fail(
          'invalid_prediction_binding',
          index,
          'session predictionSeq must refer to an earlier ledger entry',
          entry.seq,
        );
      }
      const referenced = entries[(predictionSeq as number) - 1];
      if (!referenced || referenced.type !== 'prediction') {
        return fail(
          'invalid_prediction_binding',
          index,
          'session predictionSeq must refer to a committed prediction',
          entry.seq,
        );
      }
      const predictionPayload = parsePayload(referenced.payloadJson);
      if (!predictionPayload) {
        return fail('invalid_payload_json', index, 'referenced prediction payloadJson is not a JSON object', entry.seq);
      }
      if (!sessionMatchesPredictionIdentity(payload, predictionPayload)) {
        return fail(
          'invalid_prediction_binding',
          index,
          'session identity does not match the referenced prediction',
          entry.seq,
        );
      }
      const targetDir = payload.targetDir;
      if (targetDir !== 0 && targetDir !== 1) {
        return fail('invalid_recorded_stats', index, 'session targetDir must be 0 or 1', entry.seq);
      }
      const stats = recordedBitstreamMatches(payload, targetDir);
      if (stats === false) {
        return fail(
          'invalid_recorded_stats',
          index,
          'session bits/hits/z do not match the frozen stats core',
          entry.seq,
        );
      }
    }

    previousHash = entry.entryHash;
  }

  return {
    ok: true,
    entries: entries.length,
    headHash: previousHash,
  };
}
