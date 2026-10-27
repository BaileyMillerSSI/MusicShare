import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

// Called immediately before azd deploy builds the frontend image. This is preparation time,
// not deploy completion time; no Azure identity or runtime secrets enter this artifact.
const sha = process.env.STATUS_BUILD_SHA;
const runId = process.env.STATUS_WORKFLOW_RUN_ID;
const repository = process.env.STATUS_WORKFLOW_REPOSITORY;
if (!/^[a-f0-9]{40}(?![\s\S])/.test(sha ?? '') || !/^[1-9][0-9]{0,31}(?![\s\S])/.test(runId ?? '')
    || repository !== 'BaileyMillerSSI/MusicShare') {
  throw new Error('Invalid status build metadata');
}
const metadata = { sha, builtAt: new Date().toISOString(), workflow: {
  runId, url: `https://github.com/${repository}/actions/runs/${runId}`,
} };
writeFileSync(fileURLToPath(new URL('../src/lib/server/status-build.json', import.meta.url)), JSON.stringify(metadata) + '\n');
