import { createProductionRngService } from './factory';
import { LocalCryptoRngProvider } from './providers/local';
import { RandomOrgRngProvider } from './providers/randomOrg';
import { RngService } from './service';

let applicationRng: RngService | undefined;
let anuConfigured = false;

function configuredAnuEndpoint(): string | null {
  const value = import.meta.env.VITE_ANU_RNG_ENDPOINT;
  if (typeof value !== 'string') return null;
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

/**
 * One RNG service per app runtime keeps provider rate gates shared.
 * When the ANU/proxy endpoint is not configured, the app remains usable via
 * the frozen fallback sources and records their actual source. It never labels
 * fallback data as quantum-primary data.
 */
export function getApplicationRngService(): RngService {
  if (!applicationRng) {
    const endpoint = configuredAnuEndpoint();
    anuConfigured = endpoint !== null;
    applicationRng = endpoint
      ? createProductionRngService({ anu: { endpoint } })
      : new RngService([new RandomOrgRngProvider(), new LocalCryptoRngProvider()]);
  }
  return applicationRng;
}

export function getApplicationRngConfiguration(): { anuConfigured: boolean } {
  getApplicationRngService();
  return { anuConfigured };
}
