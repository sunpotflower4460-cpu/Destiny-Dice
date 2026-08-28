import { AnuRngProvider, type AnuProviderOptions } from './providers/anu';
import { LocalCryptoRngProvider, type CryptoLike } from './providers/local';
import { RandomOrgRngProvider, type RandomOrgProviderOptions } from './providers/randomOrg';
import { RngService } from './service';

export type ProductionRngOptions = {
  anu: AnuProviderOptions;
  randomOrg?: RandomOrgProviderOptions;
  localCrypto?: CryptoLike;
};

/**
 * Frozen production order: ANU -> RANDOM.ORG -> local WebCrypto.
 * Consumers receive the actual source on every successful acquisition.
 */
export function createProductionRngService(options: ProductionRngOptions): RngService {
  return new RngService([
    new AnuRngProvider(options.anu),
    new RandomOrgRngProvider(options.randomOrg),
    new LocalCryptoRngProvider(options.localCrypto),
  ]);
}
