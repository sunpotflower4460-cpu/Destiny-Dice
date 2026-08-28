import { readFileSync } from 'node:fs';

const BUNDLE_ID = 'com.sunpotflower4460.intentiondice';

function text(path: string): string {
  return readFileSync(path, 'utf8');
}

function requireCondition(condition: boolean, message: string): void {
  if (!condition) throw new Error(message);
}

function occurrences(value: string, needle: string): number {
  return value.split(needle).length - 1;
}

function pngInfo(path: string): { width: number; height: number; colorType: number } {
  const value = readFileSync(path);
  requireCondition(value.length > 26, `${path}: PNG is too short`);
  requireCondition(value.subarray(1, 4).toString('ascii') === 'PNG', `${path}: invalid PNG signature`);
  return {
    width: value.readUInt32BE(16),
    height: value.readUInt32BE(20),
    colorType: value[25] ?? -1,
  };
}

const capacitor = text('capacitor.config.ts');
requireCondition(capacitor.includes(`appId: '${BUNDLE_ID}'`), 'Capacitor production bundle id mismatch');
requireCondition(!capacitor.includes('com.example.'), 'placeholder bundle id remains in capacitor config');

const project = text('ios/App/App.xcodeproj/project.pbxproj');
requireCondition(occurrences(project, `PRODUCT_BUNDLE_IDENTIFIER = ${BUNDLE_ID};`) === 2, 'Xcode Debug/Release bundle ids are not both production ids');
requireCondition(!project.includes('com.example.intentiondice'), 'placeholder Xcode bundle id remains');
requireCondition(occurrences(project, 'MARKETING_VERSION = 1.0;') === 2, 'expected iOS marketing version 1.0 in Debug/Release');

const appfile = text('fastlane/Appfile');
requireCondition(appfile.includes(`app_identifier('${BUNDLE_ID}')`), 'fastlane Appfile bundle id mismatch');

const smoke = text('.github/workflows/ios-smoke.yml');
const release = text('.github/workflows/ios-release.yml');
requireCondition(smoke.includes('runs-on: macos-26') && smoke.includes('test "$major" -ge 26'), 'iOS smoke must enforce Xcode 26+ on macos-26');
requireCondition(release.includes('runs-on: macos-26') && release.includes('workflow_dispatch'), 'TestFlight release must be manual on macos-26');
requireCondition(release.includes('bundle exec fastlane ios beta'), 'TestFlight release is not wired to fastlane beta');

const metadata = text('docs/store/METADATA_JA.md');
for (const forbidden of ['超常現象を証明した', '絶対に引き寄せ', '改竄不能']) {
  requireCondition(!metadata.includes(forbidden), `forbidden App Store claim found: ${forbidden}`);
}
requireCondition(metadata.includes('1年間のパーソナル実験'), 'store copy must use personal-experiment framing');

const icon = pngInfo('ios/App/App/Assets.xcassets/AppIcon.appiconset/AppIcon-512@2x.png');
requireCondition(icon.width === 1024 && icon.height === 1024, 'App Icon must be 1024x1024');
requireCondition(icon.colorType === 2, 'App Icon must be RGB without alpha');
const splashNames = ['splash-2732x2732.png', 'splash-2732x2732-1.png', 'splash-2732x2732-2.png'];
for (const name of splashNames) {
  const splash = pngInfo(`ios/App/App/Assets.xcassets/Splash.imageset/${name}`);
  requireCondition(splash.width === 2732 && splash.height === 2732, `${name} must be 2732x2732`);
  requireCondition(splash.colorType === 2, `${name} must be RGB without alpha`);
}

const worker = text('workers/qrng-proxy/src/index.ts');
requireCondition(worker.includes("request.method === 'POST' && url.pathname === '/anchors'"), 'Worker anchor POST route missing');
requireCondition(worker.includes('GET') && worker.includes('/anchors\\/([0-9a-f]{64})'), 'Worker public anchor GET route missing');
requireCondition(worker.includes("'genesisHash', 'headHash', 'headSeq', 'protocolVersion'"), 'Worker privacy-minimal anchor contract missing');
requireCondition(worker.includes('do not make a local ledger tamper-proof'), 'Worker must preserve tamper-evident wording');

console.log(JSON.stringify({
  ok: true,
  bundleId: BUNDLE_ID,
  marketingVersion: '1.0',
  appIcon: `${icon.width}x${icon.height} RGB/no-alpha`,
  xcodeGate: '26+',
  publicAnchor: 'GET /anchors/<genesisHash>',
}, null, 2));
