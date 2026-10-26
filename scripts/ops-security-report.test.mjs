import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { normalizeReports } from './ops-security-report.mjs';
const identity = { repository: 'BaileyMillerSSI/MusicShare', sha: 'a'.repeat(40), run_id: 1, npm_version: '10.9.0' };
const audit = { auditReportVersion: 2, metadata: { vulnerabilities: { critical: 0, high: 2, moderate: 0, low: 0, info: 0 } } };
const semgrep = { version: '1.182.0', results: [{ extra: { severity: 'ERROR' } }], errors: [], paths: { scanned: ['src/example.ts'] } };
test('established outputs normalize findings, unknown and partial independently', () => {
  const report = normalizeReports(audit, semgrep, identity, 1, 1);
  assert.equal(report.tools[0].counts, null);
  assert.equal(report.tools[1].counts.high, 2);
  assert.equal(report.tools[2].counts.high, 1);
  assert.equal(report.tools[2].status, 'completed');
  const failed = normalizeReports(null, null, identity, 127, 127);
  assert.ok(failed.tools.every(tool => tool.status === 'unknown' && tool.counts === null));
  assert.equal(normalizeReports(audit, { ...semgrep, errors: [{}] }, identity, 1, 1).tools[2].status, 'partial');
  assert.equal(normalizeReports({ ...audit, error: {} }, semgrep, identity, 1, 1).tools[1].counts, null);
  assert.throws(() => normalizeReports(null, null, { ...identity, repository: 'other/repo' }, 0, 0));
  assert.ok(Buffer.byteLength(JSON.stringify(report)) < 65536);
});
test('manual workflow uploads only summary with no deployment or scan automation', async () => {
  const workflow = await readFile('.github/workflows/ops-security.yml', 'utf8');
  assert.match(workflow, /on:\n  workflow_dispatch:/);
  assert.doesNotMatch(workflow, /^  (push|pull_request|schedule):/m);
  assert.match(workflow, /permissions:\n  contents: read/);
  assert.match(workflow, /path: .private\/security\/security-summary.json/);
  assert.match(workflow, /--no-restore --vulnerable --include-transitive --format json --output-version 1/);
  assert.doesNotMatch(workflow, /azd |azure\/login|semgrep ci|--config auto/);
});
