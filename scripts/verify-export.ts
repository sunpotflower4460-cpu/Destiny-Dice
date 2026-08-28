import { readFileSync } from 'node:fs';
import { loadVerifiedExperimentExport } from '../src/report/export';

const inputPath = process.argv[2];
if (!inputPath) {
  console.error('usage: pnpm verify-export <experiment.json>');
  process.exitCode = 2;
} else {
  const verified = await loadVerifiedExperimentExport(readFileSync(inputPath, 'utf8'));
  console.log(JSON.stringify({
    ok: true,
    experimentId: verified.registration.experimentId,
    entries: verified.entries.length,
    genesisHash: verified.entries[0]!.entryHash,
    headHash: verified.entries.at(-1)!.entryHash,
    protocolVersion: verified.registration.protocolVersion,
    analysisPlanVersion: verified.registration.analysisPlanVersion,
    statsVersion: verified.registration.statsVersion,
  }, null, 2));
}
