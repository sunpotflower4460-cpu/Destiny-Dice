export type RandomFill = (buffer: Uint8Array) => Uint8Array;

export function createSecureSeed(randomFill: RandomFill = (buffer) => crypto.getRandomValues(buffer)): string {
  const bytes = new Uint8Array(32);
  randomFill(bytes);
  return Array.from(bytes, (value) => value.toString(16).padStart(2, '0')).join('');
}
