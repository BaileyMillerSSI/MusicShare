import { afterEach, describe, expect, it, vi } from 'vitest';
import { GET, HEAD } from './route';

const request = (token?: string) => new Request('https://example.test/status', {
  headers: token ? { Authorization: token } : {},
});
afterEach(() => vi.unstubAllEnvs());
describe('authenticated status', () => {
  it('fails closed without current token, even with a previous token', () => {
    vi.stubEnv('STATUS_TOKEN_CURRENT', '');
    vi.stubEnv('STATUS_TOKEN_PREVIOUS', 'old');
    expect(GET(request('Bearer old')).status).toBe(503);
  });
  it('rejects missing, wrong, malformed and overlong credentials without dependencies', () => {
    vi.stubEnv('STATUS_TOKEN_CURRENT', 'current');
    const fetch = vi.spyOn(globalThis, 'fetch');
    for (const token of [undefined, 'Bearer wrong', 'Basic current', 'Bearer current,old', `Bearer ${'x'.repeat(4097)}`]) {
      const response = GET(request(token));
      expect(response.status).toBe(401);
      expect(response.headers.get('Cache-Control')).toBe('private, no-store');
      expect(response.headers.get('Access-Control-Allow-Origin')).toBeNull();
    }
    expect(fetch).not.toHaveBeenCalled();
    fetch.mockRestore();
  });
  it('accepts both rotation tokens and reports only the observed web process', async () => {
    vi.stubEnv('STATUS_TOKEN_CURRENT', 'current');
    vi.stubEnv('STATUS_TOKEN_PREVIOUS', 'previous');
    for (const token of ['current', 'previous']) {
      const response = GET(request(`Bearer ${token}`));
      expect(response.status).toBe(200);
      const body = await response.json();
      expect(body.service).toBe('musicshare');
      expect(body.health.coverage).toEqual(['web-process']);
      expect(body.deployment).toEqual({ id: null, deployedAt: null });
      expect(body.build).toEqual({ sha: null, version: null, builtAt: null });
      expect(JSON.stringify(body).length).toBeLessThan(32768);
      expect(response.headers.get('CDN-Cache-Control')).toBe('no-store');
      expect(response.headers.get('Cloudflare-CDN-Cache-Control')).toBe('no-store');
    }
    vi.stubEnv('STATUS_TOKEN_PREVIOUS', '');
    expect(GET(request('Bearer previous')).status).toBe(401);
  });
  it('authenticates HEAD and never returns a body', async () => {
    vi.stubEnv('STATUS_TOKEN_CURRENT', 'current');
    expect(HEAD(request()).status).toBe(401);
    expect(await HEAD(request('Bearer current')).text()).toBe('');
  });
  it('rejects unbounded environment labels', () => {
    vi.stubEnv('STATUS_TOKEN_CURRENT', 'current');
    vi.stubEnv('STATUS_ENVIRONMENT', 'x'.repeat(33));
    expect(GET(request('Bearer current')).status).toBe(503);
  });
});
