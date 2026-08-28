import { readFileSync, writeFileSync } from 'node:fs';
import { buildFinalReportModel, loadVerifiedExperimentExport, renderFinalReportMarkdown } from '../src/report';

const inputPath = process.argv[2];
if (!inputPath) {
  console.error('usage: pnpm generate-report <experiment.json> [output.md]');
  process.exitCode = 2;
} else {
  const verified = await loadVerifiedExperimentExport(readFileSync(inputPath, 'utf8'));
  const report = buildFinalReportModel(verified.entries, verified.registration);
  const markdown = renderFinalReportMarkdown(report);
  const outputPath = process.argv[3];
  if (outputPath) {
    writeFileSync(outputPath, markdown, 'utf8');
    console.log(JSON.stringify({ ok: true, outputPath, experimentId: report.experimentId, headHash: report.headHash }, null, 2));
  } else {
    process.stdout.write(markdown);
  }
}
