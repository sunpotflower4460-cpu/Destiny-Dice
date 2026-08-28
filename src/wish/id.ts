export function createSecureWishId(): string {
  if (typeof crypto === 'undefined' || typeof crypto.randomUUID !== 'function') {
    throw new Error('crypto.randomUUID is required to create wish IDs');
  }
  return crypto.randomUUID();
}
