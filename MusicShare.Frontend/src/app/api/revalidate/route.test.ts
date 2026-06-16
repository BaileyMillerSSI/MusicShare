import { revalidatePath } from 'next/cache';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { POST } from './route';

vi.mock('next/cache', () => ({
  revalidatePath: vi.fn(),
}));

const revalidationSecret = 'expected-secret';

function createJsonRequest(body: unknown, apiKey = revalidationSecret) {
  const headers = new Headers({ 'Content-Type': 'application/json' });

  if (apiKey) {
    headers.set('X-API-KEY', apiKey);
  }

  return new Request('https://musicshare.example.com/api/revalidate', {
    method: 'POST',
    headers,
    body: JSON.stringify(body),
  });
}

describe('POST /api/revalidate', () => {
  beforeEach(() => {
    vi.stubEnv('REVALIDATION_SECRET', revalidationSecret);
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('returns 500 when the revalidation secret is not configured', async () => {
    vi.stubEnv('REVALIDATION_SECRET', '');

    const response = await POST(createJsonRequest({ shareId: 'abc123def456' }));

    expect(response.status).toBe(500);
    await expect(response.json()).resolves.toEqual({
      error: 'Revalidation secret not configured',
    });
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it('returns 401 when the API key is missing', async () => {
    const response = await POST(createJsonRequest({ shareId: 'abc123def456' }, ''));

    expect(response.status).toBe(401);
    await expect(response.json()).resolves.toEqual({ error: 'Unauthorized' });
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it('returns 401 when the API key is invalid', async () => {
    const response = await POST(createJsonRequest({ shareId: 'abc123def456' }, 'wrong-secret'));

    expect(response.status).toBe(401);
    await expect(response.json()).resolves.toEqual({ error: 'Unauthorized' });
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it('returns 400 when the request body is not valid JSON', async () => {
    const response = await POST(
      new Request('https://musicshare.example.com/api/revalidate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-API-KEY': revalidationSecret,
        },
        body: '{',
      })
    );

    expect(response.status).toBe(400);
    await expect(response.json()).resolves.toEqual({ error: 'Invalid JSON body' });
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it('returns 400 when shareId is missing', async () => {
    const response = await POST(createJsonRequest({}));

    expect(response.status).toBe(400);
    await expect(response.json()).resolves.toEqual({ error: 'shareId is required' });
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it.each([
    ['empty string', ''],
    ['non-hex characters', 'abc123def45g'],
    ['too short', 'abc123def45'],
    ['too long', 'abc123def4567'],
    ['path traversal', '../abc123def'],
    ['path separator', 'abc123/def45'],
    ['uppercase hex', 'ABC123DEF456'],
  ])('returns 400 for malformed shareId: %s', async (_description, shareId) => {
    const response = await POST(createJsonRequest({ shareId }));

    expect(response.status).toBe(400);
    expect(revalidatePath).not.toHaveBeenCalled();
  });

  it('revalidates the encoded share path for a valid request', async () => {
    const shareId = 'abc123def456';

    const response = await POST(createJsonRequest({ shareId }));

    expect(response.status).toBe(200);
    await expect(response.json()).resolves.toEqual({ revalidated: true, shareId });
    expect(revalidatePath).toHaveBeenCalledTimes(1);
    expect(revalidatePath).toHaveBeenCalledWith(`/share/${encodeURIComponent(shareId)}`);
  });
});
