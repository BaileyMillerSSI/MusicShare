import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { normalizeReports } from './ops-security-report.mjs';
const identity = { repository: 'BaileyMillerSSI/MusicShare', sha: 'a'.repeat(40), run_id: 1, npm_version: '10.9.0' };
const finding = name => ({ name, severity: 'high', isDirect: true, via: ['synthetic-advisory'], effects: [], nodes: ['node_modules/' + name], range: '*', fixAvailable: false });
const dependencies = { prod: 2, dev: 0, optional: 0, peer: 0, peerOptional: 0, total: 2 };
const audit = { auditReportVersion: 2, vulnerabilities: { a: finding('a'), b: finding('b') }, metadata: { dependencies, vulnerabilities: { total: 2, critical: 0, high: 2, moderate: 0, low: 0, info: 0 } } };
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

test("npm malformed or contradictory results never mean clean", () => {
    const counts = {
        critical: 0,
        high: 0,
        moderate: 0,
        low: 0,
        info: 0,
        total: 0,
    };
    const valid = {
        auditReportVersion: 2,
        vulnerabilities: {},
        metadata: { dependencies, vulnerabilities: counts },
    };
    const check = (report, code = 0) =>
        normalizeReports(report, null, identity, code, 127).tools[1].status;
    assert.equal(check(valid), "completed");
    for (const bad of [
        { auditReportVersion: 2, metadata: { vulnerabilities: counts } },
        { ...valid, error: {} },
        { ...valid, vulnerabilities: { bad: { severity: "high" } } },
        {
            ...valid,
            metadata: {
                ...valid.metadata,
                vulnerabilities: { ...counts, high: 1, total: 1 },
            },
        },
    ])
        assert.equal(check(bad), "unknown");
    assert.equal(check(valid, 1), "unknown");
});

test("Semgrep malformed paths or exit contradiction remain unknown",()=>{
const valid={version:"1.182.0",results:[],errors:[],paths:{scanned:["src/a.ts"]}};
const check=(report,code=0)=>normalizeReports(null,report,identity,127,code).tools[2].status;
assert.equal(check(valid),"completed"); assert.equal(check(valid,1),"unknown");
for(const paths of [{scanned:[null]},{scanned:[{}]},{scanned:[]},{scanned:["a"],skipped:{}},{scanned:["a"],skipped:[null]}]) assert.equal(check({...valid,paths}),"unknown");
assert.equal(check({...valid,paths:{scanned:["a"],skipped:[{path:"b",reason:"unsupported"}]}}),"partial");
});
test('prototype-named Semgrep severity never escapes the bounded count schema', () => {
  for (const severity of ['__proto__', 'constructor', 'toString']) {
    const scan = normalizeReports(null, { ...semgrep, results: [{ extra: { severity } }] }, identity, 127, 1).tools[2];
    assert.equal(scan.status, 'unknown');
    assert.equal(scan.counts, null);
  }
});
