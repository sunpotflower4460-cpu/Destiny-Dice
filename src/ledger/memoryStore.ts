import type { LedgerStore, PendingLedgerEntry, StoredLedgerEntry } from './types';

function cloneEntry(entry: StoredLedgerEntry): StoredLedgerEntry {
  return { ...entry };
}

export class MemoryLedgerStore implements LedgerStore {
  private readonly entries: StoredLedgerEntry[] = [];

  async getLast(): Promise<StoredLedgerEntry | null> {
    const entry = this.entries.at(-1);
    return entry ? cloneEntry(entry) : null;
  }

  async append(entry: PendingLedgerEntry): Promise<StoredLedgerEntry> {
    const stored: StoredLedgerEntry = {
      ...entry,
      seq: this.entries.length + 1,
    };
    this.entries.push(stored);
    return cloneEntry(stored);
  }

  async list(): Promise<StoredLedgerEntry[]> {
    return this.entries.map(cloneEntry);
  }
}
