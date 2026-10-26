import { test } from 'node:test';
import assert from 'node:assert/strict';
import { normalizeDotnet } from './dotnet-audit-summary.mjs';
const expected = ['App/App.csproj'];
const targets = { 'App/App.csproj': ['net10.0'] };
const clean = () => ({ version: 1, projects: [{ path: '/checkout/App/App.csproj', frameworks: [{ framework: 'net10.0' }] }] });
test('clean, explicit project/framework report completes', () => {
  assert.deepEqual(normalizeDotnet(clean(), 0, expected, targets), { status: 'completed', counts: { critical: 0, high: 0, moderate: 0, low: 0, info: 0 } });
});
test('counts package advisory occurrences across direct and transitive results', () => {
  const report = clean();
  report.projects[0].frameworks[0].transitivePackages = [{ id: 'Fixture', resolvedVersion: '1.0', vulnerabilities: [{ severity: 'High' }] }];
  assert.equal(normalizeDotnet(report, 0, expected, targets).counts.high, 1);
});
test('failures, missing coverage and malformed severities never become zero findings', () => {
  const samples = [null, {}, { ...clean(), problems: [{ problemType: 'Warning' }] }, { ...clean(), projects: [] }, { version: 1, projects: [{ path: 'App/App.csproj' }] }];
  const invalid = clean();
  invalid.projects[0].frameworks[0].topLevelPackages = [{ id: 'Fixture', resolvedVersion: '1', vulnerabilities: [{ severity: 'Unexpected' }] }];
  samples.push(invalid);
  for (const sample of samples) assert.deepEqual(normalizeDotnet(sample, 0, expected, targets), { status: 'unknown', counts: null });
  assert.equal(normalizeDotnet(clean(), 1, expected, targets).counts, null);
  assert.equal(normalizeDotnet(clean(), 0, [...expected, 'Missing/Missing.csproj'], targets).counts, null);
});

test('rejects malformed or incomplete framework identities and explicit null package lists', () => {
  for (const framework of [{ framework: { bad: true } }, { framework: 'net9.0' }, { framework: 'net10.0', topLevelPackages: null }, { framework: 'net10.0', transitivePackages: null }]) {
    const report = clean(); report.projects[0].frameworks = [framework];
    assert.deepEqual(normalizeDotnet(report, 0, expected, targets), { status: 'unknown', counts: null });
  }
  assert.equal(normalizeDotnet(clean(), 0, expected).counts, null);
  assert.equal(normalizeDotnet(clean(), 0, expected, { 'App/App.csproj': ['net10.0', 'net9.0'] }).counts, null);
});
