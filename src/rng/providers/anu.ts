import { RateGate } from '../rateGate';
import type { RngProvider } from '../types';
import { fetchWithTimeout, type FetchLike } from './http';

export type AnuProviderOptions = {
  /**
   * Configured ANU/QRNG endpoint. No endpoint is hard-coded because Gate 0
   * froze the service location/auth/throttle as adapter configuration.
   * The endpoint must accept `length=<bytes>&type=uint8` and return
   * `{ data: number[] }`, which matches the legacy ANU JSON shape and the
   * normalization contract expected from a future app proxy.
   */
  endpoint: string;
  timeoutMs?: number;
  minIntervalMs?: number;
  headers?: HeadersInit;
  fetchFn?: FetchLike;
  now?: () => number;
  sleep?: (ms: number) => Promise<void>;
};

function appendQuery(endpoint: string, params: URLSearchParams): string {
  const separator = endpoint.includes('?') ? '&' : '?';
  return `${endpoint}${separator}${params.toString()}`;
}

function parseAnuBytes(payload: unknown, expectedLength: number): Uint8Array {
  if (typeof payload !== 'object' || payload === null || !('data' in payload)) {
    throw new Error('ANU response is missing data');
  }

  const data = (payload as { data?: unknown }).data;
  if (!Array.isArray(data) || data.length !== expectedLength) {
    throw new Error(`ANU response must contain exactly ${expectedLength} bytes`);
  }

  const bytes = new Uint8Array(expectedLength);
  for (let index = 0; index < data.length; index += 1) {
    const value = data[index];
    if (!Number.isInteger(value) || (value as number) < 0 || (value as number) > 255) {
      throw new Error(`ANU response contains an invalid byte at index ${index}`);
    }
    bytes[index] = value as number;
  }

  return bytes;
}

export class AnuRngProvider implements RngProvider {
  readonly source = 'anu' as const;

  private readonly endpoint: string;
  private readonly timeoutMs: number;
  private readonly headers: HeadersInit | undefined;
  private readonly fetchFn: FetchLike;
  private readonly gate: RateGate;

  constructor(options: AnuProviderOptions) {
    if (options.endpoint.trim().length === 0) {
      throw new Error('ANU endpoint is required');
    }

    this.endpoint = options.endpoint;
    this.timeoutMs = options.timeoutMs ?? 8_000;
    this.headers = options.headers;
    this.fetchFn = options.fetchFn ?? fetch;
    this.gate = new RateGate({
      minIntervalMs: options.minIntervalMs ?? 0,
      now: options.now,
      sleep: options.sleep,
    });
  }

  async getBytes(byteLength: number): Promise<Uint8Array> {
    if (!Number.isInteger(byteLength) || byteLength <= 0 || byteLength > 1024) {
      throw new RangeError('ANU byteLength must be an integer in [1, 1024]');
    }

    return this.gate.run(async () => {
      const params = new URLSearchParams({
        length: String(byteLength),
        type: 'uint8',
      });
      const response = await fetchWithTimeout(
        this.fetchFn,
        appendQuery(this.endpoint, params),
        { method: 'GET', headers: this.headers },
        this.timeoutMs,
      );

      if (!response.ok) {
        throw new Error(`ANU request failed with HTTP ${response.status}`);
      }

      return parseAnuBytes(await response.json(), byteLength);
    });
  }
}
