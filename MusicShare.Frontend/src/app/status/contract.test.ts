import { readFileSync, mkdtempSync, mkdirSync, copyFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import Ajv from 'ajv';
import { describe, expect, it, vi, afterEach } from 'vitest';
import { GET } from './route';
import { statusBuild } from '../../lib/server/status-metadata';

const schema = JSON.parse(readFileSync('../docs/contracts/status-v2/schema.json', 'utf8'));
const fixtures = JSON.parse(readFileSync('../docs/contracts/status-v2/fixtures.json', 'utf8'));
const ajv = new Ajv();
const validateSchema = ajv.compile(schema);
// Fixture payloads intentionally include malformed types.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
function conforms(value: any) {
  if (!validateSchema(value)) return false;
  for (const time of [value.sampledAt, value.build.builtAt, value.deployment.deployedAt]) {
    if (time !== null && (!Number.isFinite(Date.parse(time)) || new Date(time).toISOString().slice(0, 19) !== time.slice(0, 19))) return false;
  }
  const workflow = value.build.workflow;
  if (workflow && workflow.url.split('/').at(-1) !== workflow.runId) return false;
  const names = value.health.components.map((component: { name: string; state: string }) => component.name);
  if (new Set(names).size !== names.length || names.length !== value.health.coverage.length || !names.every((name: string) => value.health.coverage.includes(name))) return false;
  if (value.health.state === 'healthy' && (!names.length || value.health.components.some((component: { name: string; state: string }) => component.state !== 'healthy'))) return false;
  return Object.values(value.metrics ?? {}).every(metric => typeof metric === 'number' && Number.isFinite(metric) && metric >= 0);
}
afterEach(() => vi.unstubAllEnvs());
describe('shared status v2 conformance', () => {
  for (const fixture of fixtures.valid) it(`accepts ${fixture.name}`, () => expect(conforms(fixture.value)).toBe(true));
  for (const fixture of fixtures.invalid) it(`rejects ${fixture.name}`, () => {
    if (fixture.validation === 'schema') expect(validateSchema(fixture.value)).toBe(false);
    expect(conforms(fixture.value)).toBe(false);
  });
  it('emits a conforming local response', async () => {
    vi.stubEnv('STATUS_TOKEN_CURRENT', 'a'.repeat(32));
    const body = await GET(new Request('https://example.test/status', { headers: { authorization: `Bearer ${'a'.repeat(32)}` } })).json();
    expect(conforms(body)).toBe(true);
  });
  it('retains immutable build metadata and rejects unsafe workflow references', () => {
    const metadata = { sha: 'a'.repeat(40), builtAt: '2026-10-27T01:02:03.000Z', workflow: { runId: '123', url: 'https://github.com/BaileyMillerSSI/MusicShare/actions/runs/123' } };
    expect(statusBuild(metadata)).toEqual({ ...metadata, version: null });
    for (const url of [metadata.workflow.url + '?token=secret', metadata.workflow.url + '/jobs', metadata.workflow.url.replace('/123', '/124')]) expect(statusBuild({ ...metadata, workflow: { runId: '123', url } })).not.toHaveProperty('workflow');
    expect(statusBuild({ sha: 'bad', builtAt: '2026-02-30T01:02:03.000Z' })).toEqual({ sha: null, builtAt: null, version: null });
  });
  it('stamps the production artifact once and fails closed on invalid build inputs', () => {
    const root = mkdtempSync(join(tmpdir(), 'musicshare-status-'));
    try {
      mkdirSync(join(root, 'scripts'));
      mkdirSync(join(root, 'src/lib/server'), { recursive: true });
      copyFileSync('scripts/prepare-status-build.mjs', join(root, 'scripts/prepare-status-build.mjs'));
      const env = { ...process.env, STATUS_BUILD_SHA: 'b'.repeat(40), STATUS_WORKFLOW_RUN_ID: '987654321', STATUS_WORKFLOW_REPOSITORY: 'BaileyMillerSSI/MusicShare' };
      const before = Date.now();
      expect(spawnSync(process.execPath, [join(root, 'scripts/prepare-status-build.mjs')], { env }).status).toBe(0);
      const artifact = readFileSync(join(root, 'src/lib/server/status-build.json'), 'utf8');
      const metadata = JSON.parse(artifact);
      expect(metadata.sha).toBe(env.STATUS_BUILD_SHA);
      expect(Date.parse(metadata.builtAt)).toBeGreaterThanOrEqual(before);
      expect(Date.parse(metadata.builtAt)).toBeLessThanOrEqual(Date.now());
      expect(metadata.workflow).toEqual({ runId: '987654321', url: 'https://github.com/BaileyMillerSSI/MusicShare/actions/runs/987654321' });
      expect(statusBuild(metadata)).toEqual({ ...metadata, version: null });
      expect(spawnSync(process.execPath, [join(root, 'scripts/prepare-status-build.mjs')], { env: { ...env, STATUS_BUILD_SHA: 'invalid' } }).status).not.toBe(0);
      expect(readFileSync(join(root, 'src/lib/server/status-build.json'), 'utf8')).toBe(artifact);
    } finally { rmSync(root, { recursive: true, force: true }); }
  });
  it('prepares metadata after provisioning and immediately before deploy', () => {
    const workflow = readFileSync('../.github/workflows/ci.yml', 'utf8');
    expect(workflow.indexOf('name: Provision Infrastructure')).toBeLessThan(workflow.indexOf('name: Prepare server-only status build metadata'));
    expect(workflow).toMatch(/run: node MusicShare.Frontend\/scripts\/prepare-status-build.mjs\s+- name: Deploy Application\s+run: azd deploy --no-prompt/);
  });
});
