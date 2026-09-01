import { describe, expect, it, vi } from 'vitest';

const imageResponse = vi.fn(function ImageResponse(this: Response, _element: unknown, options: { width: number; height: number }) {
  return new Response('image', { headers: { 'x-image-width': String(options.width), 'x-image-height': String(options.height) } });
});
const getPublicMetrics = vi.fn();

vi.mock('next/og', () => ({ ImageResponse: imageResponse }));
vi.mock('../../../lib/server/publicMetrics', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../lib/server/publicMetrics')>();
  return { ...actual, getPublicMetrics };
});

const { GET, dynamic } = await import('./route');

describe('GET /metrics/share-image', () => {
  it('uses the authoritative snapshot with fixed PNG dimensions and cache behavior', async () => {
    getPublicMetrics.mockResolvedValue({ available: true, metrics: { totalCompletedSongs: 4, serviceCounts: [], recentSongs: [], weeklyCompletedSongs: [] } });
    const response = await GET();
    expect(dynamic).toBe('force-dynamic');
    expect(getPublicMetrics).toHaveBeenCalledWith();
    expect(imageResponse).toHaveBeenCalledWith(expect.anything(), { width: 1200, height: 630 });
    expect(response.headers.get('Content-Type')).toBe('image/png');
    expect(response.headers.get('Cache-Control')).toBe('public, max-age=300, s-maxage=300');
  });

  it('renders the generic fallback when the snapshot is unavailable', async () => {
    getPublicMetrics.mockResolvedValue({ available: false, metrics: { totalCompletedSongs: 0, serviceCounts: [], recentSongs: [], weeklyCompletedSongs: [] } });
    await GET();
    const element = imageResponse.mock.calls.at(-1)?.[0] as { props: { summary?: unknown } };
    expect(element.props.summary).toBeUndefined();
  });
});
