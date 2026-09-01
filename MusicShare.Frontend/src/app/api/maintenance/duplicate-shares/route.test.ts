import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { POST } from './route';

const secret = 'maintenance-secret';
const valid = { firstShareId: 'aaaaaaaaaaaa', secondShareId: 'bbbbbbbbbbbb', mode: 'dry-run' };
const request = (body: unknown, key = secret) => new Request('https://example.test/api/maintenance/duplicate-shares', { method: 'POST', headers: { 'content-type': 'application/json', ...(key ? { 'X-MAINTENANCE-KEY': key } : {}) }, body: JSON.stringify(body) });

describe('POST /api/maintenance/duplicate-shares', () => {
  beforeEach(() => { vi.stubEnv('MAINTENANCE_SECRET', secret); vi.stubEnv('services__api__https__0', 'http://api.internal'); vi.stubGlobal('fetch', vi.fn()); });
  afterEach(() => { vi.unstubAllEnvs(); vi.unstubAllGlobals(); });

  it('fails closed for missing configuration, bad keys, and malformed input', async () => {
    vi.stubEnv('MAINTENANCE_SECRET', '');
    expect((await POST(request(valid))).status).toBe(503);
    vi.stubEnv('MAINTENANCE_SECRET', secret);
    expect((await POST(request(valid, 'wrong'))).status).toBe(401);
    expect((await POST(request({ ...valid, extra: true }))).status).toBe(400);
    expect(global.fetch).not.toHaveBeenCalled();
  });

  it('forwards only a validated fixed reconciliation request', async () => {
    vi.mocked(global.fetch).mockResolvedValue(new Response(JSON.stringify({ success: true, changed: false, affectedShareCount: 2, operationId: `reconcile-${'a'.repeat(64)}`, fingerprint: 'a'.repeat(64), canonicalShareId: 'aaaaaaaaaaaa', aliasShareId: 'bbbbbbbbbbbb', sharedIdentities: [{ serviceType: 1, serviceSongId: 'spotify-track' }], internalDetail: 'must not escape' }), { status: 200 }));
    const response = await POST(request(valid));
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ success: true, changed: false, affectedShareCount: 2, operationId: `reconcile-${'a'.repeat(64)}`, fingerprint: 'a'.repeat(64), canonicalShareId: 'aaaaaaaaaaaa', aliasShareId: 'bbbbbbbbbbbb', sharedIdentities: [{ serviceType: 1, serviceSongId: 'spotify-track' }] });
    expect(global.fetch).toHaveBeenCalledWith('http://api.internal/internal/maintenance/duplicate-shares/reconcile', expect.objectContaining({ method: 'POST', headers: expect.objectContaining({ 'X-MAINTENANCE-KEY': secret }) }));
  });

  it('rejects coercible values and malformed backend responses', async () => {
    expect((await POST(request({ ...valid, firstShareId: { toString: () => 'aaaaaaaaaaaa' } }))).status).toBe(400);
    expect((await POST(request({ ...valid, canonicalShareId: 1 }))).status).toBe(400);
    vi.mocked(global.fetch).mockResolvedValue(new Response(JSON.stringify({ success: 'true', changed: false }), { status: 200 }));
    expect((await POST(request(valid))).status).toBe(400);
    vi.mocked(global.fetch).mockResolvedValue(new Response(JSON.stringify({ success: true, changed: false, affectedShareCount: 2, operationId: `reconcile-${'a'.repeat(64)}`, fingerprint: 'a'.repeat(64), canonicalShareId: 'aaaaaaaaaaaa', aliasShareId: 'bbbbbbbbbbbb', sharedIdentities: [{ serviceType: 0, serviceSongId: 'spotify-track' }] }), { status: 200 }));
    expect((await POST(request(valid))).status).toBe(400);
    vi.mocked(global.fetch).mockResolvedValue(new Response(JSON.stringify({ success: false, changed: false, affectedShareCount: 1, error: 'bad count' }), { status: 200 }));
    expect((await POST(request(valid))).status).toBe(400);
  });
});
