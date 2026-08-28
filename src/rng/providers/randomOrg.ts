import { RateGate } from '../rateGate';
import type { RngProvider } from '../types';
import { fetchWithTimeout, type FetchLike } from './http';

export const DEFAULT_RANDOM_ORG_ENDPOINT = 'https://www.random.org/integers/';

export type RandomOrgProviderOptions = {
  endpoint?: string;
  timeoutMs?: number;
  minIntervalMs?: number;
  fetchFn?: FetchLike;
  now?: () => number;
  sleep?: (ms: number) => Promise<void>;
};

function appendQuery(endpoint: string, params: URLSearchParams): string {
  const separator = endpoint.includes('?') ? '&' : '?';
  return `${endpoint}${separator}${params.toString()}`;
}

function parseRandomOrgBytes(text: string, expectedLength: number): Uint8Array {
  const tokens = text.trim().length === 0 ? [] : text.trim().split(/\s+/);
  if (tokens.length !== expectedLength) {
    throw new Error(`RANDOM.ORG response must contain exactly ${expectedLength} integers`);
  }

  const bytes = new Uint8Array(expectedLength);
  for (let index = 0; index < tokens.length; index += 1) {
    const token = tokens[index]!;
    if (!/^\d+$/.test(token)) {
      throw new Error(`RANDOM.ORG response contains a non-integer at index ${index}`);
    }
    const value = Number(token);
    if (!Number.isInteger(value) || value < 0 || value > 255) {
      throw new Error(`RANDOM.ORG response contains an invalid byte at index ${index}`);
    }
    bytes[index] = value;
  }

  return bytes;
}

export class RandomOrgRngProvider implements RngProvider {
  readonly source = 'randomorg' as const;

  private readonly endpoint: string;
  private readonly timeoutMs: number;
  private readonly fetchFn: FetchLike;
  private readonly gate: RateGate;

  constructor(options: RandomOrgProviderOptions = {}) {
    this.endpoint = options.endpoint ?? DEFAULT_RANDOM_ORG_ENDPOINT;
    this.timeoutMs = options.timeoutMs ?? 8_000;
    this.fetchFn = options.fetchFn ?? fetch;
    this.gate = new RateGate({
      minIntervalMs: options.minIntervalMs ?? 0,
      now: options.now,
      sleep: options.sleep,
    });
  }

  async getBytes(byteLength: number): Promise<Uint8Array> {
    if (!Number.isInteger(byteLength) || byteLength <= 0 || byteLength > 10_000) {
      throw new RangeError('RANDOM.ORG byteLength must be an integer in [1, 10000]');
    }

    return this.gate.run(async () => {
      const params = new URLSearchParams({
        num: String(byteLength),
        min: '0',
        max: '255',
        col: '1',
        base: '10',
        format: 'plain',
        rnd: 'new',
      });
      const response = await fetchWithTimeout(
        this.fetchFn,
        appendQuery(this.endpoint, params),
        { method: 'GET' },
        this.timeoutMs,
      );

      if (!response.ok) {
        throw new Error(`RANDOM.ORG request failed with HTTP ${response.status}`);
      }

      const text = await response.text();
      if (/^Error:/im.test(text)) {
        throw new Error(`RANDOM.ORG returned an error response: ${text.trim()}`);
      }

      return parseRandomOrgBytes(text, byteLength);
    });
  }
}
