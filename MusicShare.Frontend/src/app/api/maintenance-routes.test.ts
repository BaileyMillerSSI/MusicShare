import { revalidatePath } from 'next/cache';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { POST as reindexAll } from './reindex/all/route';
import { POST as reindexSong } from './reindex/song/[songId]/route';
import { POST as revalidate } from './revalidate/route';
import { POST as revalidateAll } from './revalidate-all/route';

vi.mock('next/cache', () => ({
  revalidatePath: vi.fn(),
}));

const originalEnv = { ...process.env };

function request(apiKey: string | null = 'secret', body?: unknown) {
  return new Request('https://frontend.test/api/maintenance', {
    method: 'POST',
    headers: apiKey ? { 'X-API-KEY': apiKey } : undefined,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

async function json(response: Response) {
  return (await response.json()) as Record<string, unknown>;
}

describe('maintenance API routes', () => {
  beforeEach(() => {
    process.env = { ...originalEnv };
    vi.mocked(revalidatePath).mockClear();
    global.fetch = vi.fn();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    process.env = originalEnv;
  });

  describe('revalidate', () => {
    it('rejects requests when the secret is missing', async () => {
      delete process.env.REVALIDATION_SECRET;

      const response = await revalidate(request('secret', { shareId: 'abc123def456' }));

      expect(response.status).toBe(500);
      await expect(json(response)).resolves.toEqual({ error: 'Revalidation secret not configured' });
    });

    it('rejects unauthorized requests', async () => {
      process.env.REVALIDATION_SECRET = 'secret';

      const response = await revalidate(request('wrong', { shareId: 'abc123def456' }));

      expect(response.status).toBe(401);
      await expect(json(response)).resolves.toEqual({ error: 'Unauthorized' });
    });

    it('rejects malformed share IDs', async () => {
      process.env.REVALIDATION_SECRET = 'secret';

      const response = await revalidate(request('secret', { shareId: '../abc123def456' }));

      expect(response.status).toBe(400);
      expect(revalidatePath).not.toHaveBeenCalled();
    });

    it('revalidates the encoded share path for a valid request', async () => {
      process.env.REVALIDATION_SECRET = 'secret';

      const response = await revalidate(request('secret', { shareId: 'abc123def456' }));

      expect(response.status).toBe(200);
      expect(revalidatePath).toHaveBeenCalledWith('/share/abc123def456');
      await expect(json(response)).resolves.toEqual({ revalidated: true, shareId: 'abc123def456' });
    });
  });

  describe('revalidate-all', () => {
    it('rejects requests when the secret is missing', async () => {
      delete process.env.REVALIDATION_SECRET;

      const response = await revalidateAll(request('secret'));

      expect(response.status).toBe(500);
      await expect(json(response)).resolves.toEqual({ error: 'Revalidation secret not configured' });
    });

    it('rejects unauthorized requests', async () => {
      process.env.REVALIDATION_SECRET = 'secret';

      const response = await revalidateAll(request('wrong'));

      expect(response.status).toBe(401);
      await expect(json(response)).resolves.toEqual({ error: 'Unauthorized' });
    });

    it('rejects requests when the API target is missing', async () => {
      process.env.REVALIDATION_SECRET = 'secret';
      delete process.env.services__api__https__0;
      delete process.env.services__api__http__0;

      const response = await revalidateAll(request('secret'));

      expect(response.status).toBe(500);
      await expect(json(response)).resolves.toEqual({ error: 'API base URL not configured or invalid' });
    });

    it('rejects malformed share IDs returned by the backend', async () => {
      process.env.REVALIDATION_SECRET = 'secret';
      process.env.services__api__https__0 = 'https://api.test';
      vi.mocked(fetch).mockResolvedValue({
        ok: true,
        json: async () => ({ shareIds: ['abc123def456', '../bad'] }),
      } as Response);

      const response = await revalidateAll(request('secret'));

      expect(response.status).toBe(502);
      expect(revalidatePath).not.toHaveBeenCalled();
    });

    it('returns only summary data after revalidating all shares', async () => {
      process.env.REVALIDATION_SECRET = 'secret';
      process.env.services__api__https__0 = 'https://api.test';
      vi.mocked(fetch).mockResolvedValue({
        ok: true,
        json: async () => ({ shareIds: ['abc123def456', '789abc012def'] }),
      } as Response);

      const response = await revalidateAll(request('secret'));

      expect(response.status).toBe(200);
      expect(fetch).toHaveBeenCalledWith(new URL('/internal/share/ids', 'https://api.test'));
      expect(revalidatePath).toHaveBeenCalledWith('/share/abc123def456');
      expect(revalidatePath).toHaveBeenCalledWith('/share/789abc012def');
      await expect(json(response)).resolves.toEqual({ revalidated: true, count: 2 });
    });
  });

  describe('reindex-all', () => {
    it('rejects requests when the secret is missing', async () => {
      delete process.env.REINDEX_API_KEY;

      const response = await reindexAll(request('secret'));

      expect(response.status).toBe(500);
      await expect(json(response)).resolves.toEqual({ error: 'Re-index API key not configured' });
    });

    it('rejects unauthorized requests', async () => {
      process.env.REINDEX_API_KEY = 'secret';

      const response = await reindexAll(request('wrong'));

      expect(response.status).toBe(401);
      await expect(json(response)).resolves.toEqual({ error: 'Unauthorized' });
    });

    it('rejects requests when API_URL is missing', async () => {
      process.env.REINDEX_API_KEY = 'secret';
      delete process.env.API_URL;

      const response = await reindexAll(request('secret'));

      expect(response.status).toBe(500);
      await expect(json(response)).resolves.toEqual({ error: 'API_URL not configured or invalid' });
    });

    it('forwards valid requests to the protected backend target', async () => {
      process.env.REINDEX_API_KEY = 'secret';
      process.env.API_URL = 'https://api.test';
      vi.mocked(fetch).mockResolvedValue({
        status: 200,
        json: async () => ({ success: true, count: 3 }),
      } as Response);

      const response = await reindexAll(request('secret'));

      expect(response.status).toBe(200);
      expect(fetch).toHaveBeenCalledWith(new URL('/internal/reindex/all', 'https://api.test'), {
        method: 'POST',
        headers: { 'X-API-KEY': 'secret' },
      });
      await expect(json(response)).resolves.toEqual({ success: true, count: 3 });
    });
  });

  describe('reindex-song', () => {
    it('rejects requests when the secret is missing', async () => {
      delete process.env.REINDEX_API_KEY;

      const response = await reindexSong(request('secret'), {
        params: Promise.resolve({ songId: '507f1f77bcf86cd799439011' }),
      });

      expect(response.status).toBe(500);
      await expect(json(response)).resolves.toEqual({ error: 'Re-index API key not configured' });
    });

    it('rejects unauthorized requests', async () => {
      process.env.REINDEX_API_KEY = 'secret';

      const response = await reindexSong(request('wrong'), {
        params: Promise.resolve({ songId: '507f1f77bcf86cd799439011' }),
      });

      expect(response.status).toBe(401);
      await expect(json(response)).resolves.toEqual({ error: 'Unauthorized' });
    });

    it('rejects malformed song IDs', async () => {
      process.env.REINDEX_API_KEY = 'secret';

      const response = await reindexSong(request('secret'), {
        params: Promise.resolve({ songId: '../507f1f77bcf86cd799439011' }),
      });

      expect(response.status).toBe(400);
      expect(fetch).not.toHaveBeenCalled();
    });

    it('rejects requests when API_URL is invalid', async () => {
      process.env.REINDEX_API_KEY = 'secret';
      process.env.API_URL = 'file:///tmp/api';

      const response = await reindexSong(request('secret'), {
        params: Promise.resolve({ songId: '507f1f77bcf86cd799439011' }),
      });

      expect(response.status).toBe(500);
      await expect(json(response)).resolves.toEqual({ error: 'API_URL not configured or invalid' });
      expect(fetch).not.toHaveBeenCalled();
    });

    it('forwards valid song IDs to the protected backend target', async () => {
      process.env.REINDEX_API_KEY = 'secret';
      process.env.API_URL = 'https://api.test';
      vi.mocked(fetch).mockResolvedValue({
        status: 200,
        json: async () => ({ success: true, found: true }),
      } as Response);

      const response = await reindexSong(request('secret'), {
        params: Promise.resolve({ songId: '507f1f77bcf86cd799439011' }),
      });

      expect(response.status).toBe(200);
      expect(fetch).toHaveBeenCalledWith(
        new URL('/internal/reindex/song/507f1f77bcf86cd799439011', 'https://api.test'),
        {
          method: 'POST',
          headers: { 'X-API-KEY': 'secret' },
        }
      );
      await expect(json(response)).resolves.toEqual({ success: true, found: true });
    });
  });
});
