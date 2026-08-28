import { LEDGER_ENTRY_TYPES, type LedgerEntryType } from '../db/schema';
import type { StoredLedgerEntry } from './types';

const CSV_HEADER = ['seq', 'type', 'payload_json', 'created_at', 'prev_hash', 'entry_hash'] as const;

function isLedgerEntryType(value: string): value is LedgerEntryType {
  return (LEDGER_ENTRY_TYPES as readonly string[]).includes(value);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function requireString(value: unknown, label: string): string {
  if (typeof value !== 'string') {
    throw new TypeError(`${label} must be a string`);
  }
  return value;
}

function parseEntryObject(value: unknown, index: number): StoredLedgerEntry {
  if (!isRecord(value)) {
    throw new TypeError(`Ledger JSON entry ${index} must be an object`);
  }

  const seq = value.seq;
  const type = value.type;
  if (!Number.isSafeInteger(seq) || (seq as number) <= 0) {
    throw new TypeError(`Ledger JSON entry ${index} has invalid seq`);
  }
  if (typeof type !== 'string' || !isLedgerEntryType(type)) {
    throw new TypeError(`Ledger JSON entry ${index} has invalid type`);
  }

  return {
    seq: seq as number,
    type,
    payloadJson: requireString(value.payloadJson, `Ledger JSON entry ${index} payloadJson`),
    createdAt: requireString(value.createdAt, `Ledger JSON entry ${index} createdAt`),
    prevHash: requireString(value.prevHash, `Ledger JSON entry ${index} prevHash`),
    entryHash: requireString(value.entryHash, `Ledger JSON entry ${index} entryHash`),
  };
}

export function exportLedgerJson(entries: readonly StoredLedgerEntry[]): string {
  return JSON.stringify(entries, null, 2);
}

export function importLedgerJson(text: string): StoredLedgerEntry[] {
  const parsed: unknown = JSON.parse(text);
  if (!Array.isArray(parsed)) {
    throw new TypeError('Ledger JSON export must be an array');
  }
  return parsed.map((entry, index) => parseEntryObject(entry, index));
}

function quoteCsv(value: string | number): string {
  const text = String(value);
  return `"${text.replaceAll('"', '""')}"`;
}

export function exportLedgerCsv(entries: readonly StoredLedgerEntry[]): string {
  const rows = entries.map((entry) =>
    [entry.seq, entry.type, entry.payloadJson, entry.createdAt, entry.prevHash, entry.entryHash]
      .map(quoteCsv)
      .join(','),
  );
  return [CSV_HEADER.join(','), ...rows].join('\r\n');
}

function parseCsv(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let field = '';
  let inQuotes = false;

  const pushRow = (): void => {
    row.push(field);
    rows.push(row);
    row = [];
    field = '';
  };

  for (let index = 0; index < text.length; index += 1) {
    const char = text[index]!;
    if (inQuotes) {
      if (char === '"') {
        if (text[index + 1] === '"') {
          field += '"';
          index += 1;
        } else {
          inQuotes = false;
        }
      } else {
        field += char;
      }
      continue;
    }

    if (char === '"') {
      if (field.length !== 0) {
        throw new TypeError('Invalid CSV quote placement');
      }
      inQuotes = true;
    } else if (char === ',') {
      row.push(field);
      field = '';
    } else if (char === '\r' && text[index + 1] === '\n') {
      pushRow();
      index += 1;
    } else if (char === '\n') {
      pushRow();
    } else {
      field += char;
    }
  }

  if (inQuotes) {
    throw new TypeError('Unterminated CSV quoted field');
  }
  if (field.length > 0 || row.length > 0) {
    pushRow();
  }
  return rows;
}

export function importLedgerCsv(text: string): StoredLedgerEntry[] {
  const rows = parseCsv(text);
  const header = rows.shift();
  if (!header || header.length !== CSV_HEADER.length || header.some((field, index) => field !== CSV_HEADER[index])) {
    throw new TypeError('Ledger CSV header does not match the expected schema');
  }

  return rows.map((row, index) => {
    if (row.length !== CSV_HEADER.length) {
      throw new TypeError(`Ledger CSV row ${index + 2} has ${row.length} fields`);
    }
    const seq = Number(row[0]);
    const type = row[1]!;
    if (!Number.isSafeInteger(seq) || seq <= 0) {
      throw new TypeError(`Ledger CSV row ${index + 2} has invalid seq`);
    }
    if (!isLedgerEntryType(type)) {
      throw new TypeError(`Ledger CSV row ${index + 2} has invalid type`);
    }

    return {
      seq,
      type,
      payloadJson: row[2]!,
      createdAt: row[3]!,
      prevHash: row[4]!,
      entryHash: row[5]!,
    };
  });
}
