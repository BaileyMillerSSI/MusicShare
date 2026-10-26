import { test } from 'node:test';
import assert from 'node:assert/strict';
import { normalizeDotnet } from './dotnet-audit-summary.mjs';
const expected = ['App/App.csproj'];
const clean = () => ({ version: 1, projects: [{ path: '/checkout/App/App.csproj', frameworks: [{ framework: 'net10.0' }] }] });
test('clean, explicit project/framework report completes', () => {
  assert.deepEqual(normalizeDotnet(clean(), 0, expected), { status: 'completed', counts: { critical: 0, high: 0, moderate: 0, low: 0, info: 0 } });
});
test('counts package advisory occurrences across direct and transitive results', () => {
  const report = clean();
  report.projects[0].frameworks[0].transitivePackages = [{ id: 'Fixture', resolvedVersion: '1.0', vulnerabilities: [{ severity: 'High' }] }];
  assert.equal(normalizeDotnet(report, 0, expected).counts.high, 1);
});
test('failures, missing coverage and malformed severities never become zero findings', () => {
  const samples = [null, {}, { ...clean(), problems: [{ problemType: 'Warning' }] }, { ...clean(), projects: [] }, { version: 1, projects: [{ path: 'App/App.csproj' }] }];
  const invalid = clean();
  invalid.projects[0].frameworks[0].topLevelPackages = [{ id: 'Fixture', resolvedVersion: '1', vulnerabilities: [{ severity: 'Unexpected' }] }];
  samples.push(invalid);
  for (const sample of samples) assert.deepEqual(normalizeDotnet(sample, 0, expected), { status: 'unknown', counts: null });
  assert.equal(normalizeDotnet(clean(), 1, expected).counts, null);
  assert.equal(normalizeDotnet(clean(), 0, [...expected, 'Missing/Missing.csproj']).counts, null);
});
