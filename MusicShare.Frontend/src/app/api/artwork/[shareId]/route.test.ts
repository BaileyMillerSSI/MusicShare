import { beforeEach, describe, expect, it, vi } from 'vitest';

import { GET } from './route';

describe('GET /api/artwork/[shareId]', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    delete process.env.services__api__https__0;
    delete process.env.services__api__http__0;
  });

  it('returns the provider artwork with a cacheable image response', async () => {
    const apiResponse = new Response(
      JSON.stringify({ song: { artworkUrl: 'https://images.example.com/artwork.jpg' } }),
      { headers: { 'content-type': 'application/json' } }
    );
    const artworkResponse = new Response('image bytes', {
      headers: { 'content-length': '11', 'content-type': 'image/jpeg; charset=utf-8' },
    });
    const fetchMock = vi.spyOn(global, 'fetch')
      .mockResolvedValueOnce(apiResponse)
      .mockResolvedValueOnce(artworkResponse);

    const response = await GET(new Request('https://musicshare.example.com/api/artwork/abc'), {
      params: Promise.resolve({ shareId: 'abc' }),
    });

    expect(response.status).toBe(200);
    expect(response.headers.get('content-type')).toBe('image/jpeg');
    expect(response.headers.get('content-length')).toBe('11');
    expect(response.headers.get('cache-control')).toContain('s-maxage=86400');
    expect(await response.text()).toBe('image bytes');
    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      'http://localhost:5222/api/share/abc',
      expect.objectContaining({ signal: expect.any(AbortSignal) })
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      'https://images.example.com/artwork.jpg',
      expect.objectContaining({ signal: expect.any(AbortSignal) })
    );
  });

  it('uses the HTTPS Aspire API endpoint when configured', async () => {
    process.env.services__api__https__0 = 'https://api.example.com';
    vi.spyOn(global, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ song: { title: 'No Artwork' } }), {
        headers: { 'content-type': 'application/json' },
      })
    );

    const response = await GET(new Request('https://musicshare.example.com/api/artwork/id'), {
      params: Promise.resolve({ shareId: 'id with spaces' }),
    });

    expect(response.status).toBe(404);
    expect(global.fetch).toHaveBeenCalledWith(
      'https://api.example.com/api/share/id%20with%20spaces',
      expect.objectContaining({ signal: expect.any(AbortSignal) })
    );
  });

  it('returns not found when the share has no artwork', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ song: { title: 'No Artwork' } }))
    );

    const response = await GET(new Request('https://musicshare.example.com/api/artwork/no-art'), {
      params: Promise.resolve({ shareId: 'no-art' }),
    });

    expect(response.status).toBe(404);
  });

  it('rejects non-image provider responses', async () => {
    vi.spyOn(global, 'fetch')
      .mockResolvedValueOnce(new Response(JSON.stringify({ song: { artworkUrl: 'https://images.example.com/art' } })))
      .mockResolvedValueOnce(new Response('not an image', { headers: { 'content-type': 'text/plain' } }));

    const response = await GET(new Request('https://musicshare.example.com/api/artwork/not-image'), {
      params: Promise.resolve({ shareId: 'not-image' }),
    });

    expect(response.status).toBe(415);
  });

  it('returns a bad request for an unsupported artwork URL', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ song: { artworkUrl: 'file:///tmp/art.jpg' } }))
    );

    const response = await GET(new Request('https://musicshare.example.com/api/artwork/invalid'), {
      params: Promise.resolve({ shareId: 'invalid' }),
    });

    expect(response.status).toBe(400);
  });

  it('returns a bad gateway when an upstream request fails', async () => {
    vi.spyOn(global, 'fetch').mockRejectedValue(new Error('upstream unavailable'));

    const response = await GET(new Request('https://musicshare.example.com/api/artwork/error'), {
      params: Promise.resolve({ shareId: 'error' }),
    });

    expect(response.status).toBe(502);
  });
});
