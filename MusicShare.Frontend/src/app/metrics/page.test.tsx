import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MusicServiceType } from '../../lib/api';

const fetchMock = vi.fn();
vi.stubGlobal('fetch', fetchMock);
const { default: MetricsPage, dynamic, generateMetadata } = await import('./page');

afterEach(() => vi.clearAllMocks());

describe('MetricsPage', () => {
  it('is rendered from the internal metrics snapshot at request time', () => {
    expect(dynamic).toBe('force-dynamic');
  });

  it('renders resolved platform link counts and canonical share links', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ totalCompletedSongs: 1, serviceCounts: [
      { service: MusicServiceType.Spotify, count: 1 }, { service: MusicServiceType.YouTubeMusic, count: 1 },
    ], recentSongs: [{ songId: 'song-1', shareId: 'abc123def456', title: 'Song', artists: ['Artist'], sourceService: MusicServiceType.Spotify, createdAt: '2026-01-01T00:00:00Z' }] }) });
    render(await MetricsPage());
    expect(screen.getByText('Songs')).toBeInTheDocument();
    expect(screen.getByText('Spotify links')).toBeInTheDocument();
    expect(screen.getByText('YouTube Music links')).toBeInTheDocument();
    expect(screen.queryByText('Apple Music')).not.toBeInTheDocument();
    expect(screen.queryByText(/source platform/i)).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: /song/i })).toHaveAttribute('href', '/share/abc123def456');
    expect(document.body.textContent).not.toContain('completed songs');
  });

  it('renders the current  daily change and simple  daily chart including zeroes', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ totalCompletedSongs: 6, serviceCounts: [], recentSongs: [], dailyCompletedSongs: [
      { dayStart: '2026-01-04T00:00:00Z', count: 0 }, { dayStart: '2026-01-05T00:00:00Z', count: 2 },
      { dayStart: '2026-01-06T00:00:00Z', count: 0 }, { dayStart: '2026-01-07T00:00:00Z', count: 1 },
      { dayStart: '2026-01-08T00:00:00Z', count: 0 }, { dayStart: '2026-01-09T00:00:00Z', count: 0 },
      { dayStart: '2026-01-10T00:00:00Z', count: 3 },
    ] }) });
    render(await MetricsPage());

    expect(screen.getByText('+6 in the last 7 days')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Songs added in the last 7 days' })).toBeInTheDocument();
    expect(screen.getByText('Days use UTC calendar boundaries.')).toBeInTheDocument();
    const dailyChart = screen.getByRole('list', { name: /songs added in the last 7 days, utc calendar days/i });
    const [firstBucket, secondBucket] = Array.from(dailyChart.querySelectorAll('li'));
    expect(screen.getByText('01-04')).toHaveAttribute('dateTime', '2026-01-04T00:00:00Z');
    expect(firstBucket?.querySelector('[aria-hidden="true"]')).toHaveStyle({ height: '0%' });
    expect(screen.getByText('01-05')).toHaveAttribute('dateTime', '2026-01-05T00:00:00Z');
    expect(secondBucket?.querySelector('[aria-hidden="true"]')).toHaveStyle({ height: '67%' });
    expect(screen.getByText('Today')).toBeInTheDocument();
  });

  it('supports snapshots created before  daily metrics were stored', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ totalCompletedSongs: 1, serviceCounts: [], recentSongs: [] }) });
    render(await MetricsPage());
    expect(screen.getByText('+0 in the last 7 days')).toBeInTheDocument();
    expect(screen.getByText(/daily song data is not available yet/i)).toBeInTheDocument();
  });

  it('renders a safe empty state when the internal API is unavailable', async () => {
    fetchMock.mockRejectedValue(new Error('offline'));
    render(await MetricsPage());
    expect(screen.getByText(/no songs yet/i)).toBeInTheDocument();
  });

  it('preserves backend newest-first order and caps the rendered list at 20', async () => {
    const recentSongs = Array.from({ length: 21 }, (_, index) => ({ songId: `song-${index}`, shareId: `share-${index}`, title: `Song ${index}`, artists: ['Artist'], sourceService: MusicServiceType.Spotify, createdAt: '2026-01-01T00:00:00Z' }));
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ totalCompletedSongs: 21, serviceCounts: [], recentSongs }) });

    render(await MetricsPage());

    expect(screen.getAllByRole('link', { name: /song/i }).filter((link) => link.getAttribute('href')?.startsWith('/share/'))).toHaveLength(20);
    expect(screen.getByRole('link', { name: /song 0/i })).toHaveAttribute('href', '/share/share-0');
    expect(screen.queryByRole('link', { name: /song 20/i })).not.toBeInTheDocument();
  });

  it('renders an all-zero  daily chart without errors', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ totalCompletedSongs: 0, serviceCounts: [], recentSongs: [], dailyCompletedSongs: [
      { dayStart: '2026-01-04T00:00:00Z', count: 0 }, { dayStart: '2026-01-05T00:00:00Z', count: 0 },
      { dayStart: '2026-01-06T00:00:00Z', count: 0 }, { dayStart: '2026-01-07T00:00:00Z', count: 0 },
      { dayStart: '2026-01-08T00:00:00Z', count: 0 }, { dayStart: '2026-01-09T00:00:00Z', count: 0 },
      { dayStart: '2026-01-10T00:00:00Z', count: 0 },
    ] }) });
    render(await MetricsPage());

    expect(screen.getAllByText('0')).toHaveLength(10);
    const dailyChart = screen.getByRole('list', { name: /songs added in the last 7 days, utc calendar days/i });
    expect(dailyChart.querySelectorAll('li')).toHaveLength(7);
    expect(screen.getByText('01-04')).toHaveAttribute('dateTime', '2026-01-04T00:00:00Z');
    await expect(generateMetadata()).resolves.toMatchObject({
      description: '0 songs, 0 Spotify links, 0 YouTube Music links, and 0 added in the last 7 days.',
    });
  });

  it.each([
    { totalCompletedSongs: 1, serviceCounts: 'invalid', recentSongs: [] },
    { totalCompletedSongs: 1, serviceCounts: [], recentSongs: [{ songId: 3 }] },
    { totalCompletedSongs: 1, serviceCounts: [], recentSongs: [], dailyCompletedSongs: [{ dayStart: 'not-a-date', count: 1 }] },
  ])('renders a safe empty state for malformed successful payloads', async (payload) => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => payload });
    render(await MetricsPage());
    expect(screen.getByText(/no songs yet/i)).toBeInTheDocument();
  });

  it('renders a safe empty state for unsuccessful responses', async () => {
    fetchMock.mockResolvedValue({ ok: false });
    render(await MetricsPage());
    expect(screen.getByText(/no songs yet/i)).toBeInTheDocument();
  });

  it('generates factual, versioned canonical social metadata for a valid snapshot', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ totalCompletedSongs: 289, generatedAt: '2026-08-31T12:00:00Z', serviceCounts: [
      { service: MusicServiceType.Spotify, count: 289 }, { service: MusicServiceType.YouTubeMusic, count: 268 },
    ], recentSongs: [], dailyCompletedSongs: Array.from({ length: 7 }, (_, index) => ({ dayStart: `2026-08-${String(25 + index).padStart(2, '0')}T00:00:00Z`, count: index === 6 ? 6 : 0 })) }) });
    const metadata = await generateMetadata();
    const openGraphImages = metadata.openGraph?.images;
    const image = (Array.isArray(openGraphImages) ? openGraphImages[0] : openGraphImages) as { url?: string; width?: number; height?: number; type?: string; alt?: string };
    expect(metadata.alternates?.canonical).toBe('https://music.baileymiller.dev/metrics');
    expect(metadata.description).toBe('289 songs, 289 Spotify links, 268 YouTube Music links, and 6 added in the last 7 days.');
    expect(metadata.openGraph).toMatchObject({ type: 'website', url: 'https://music.baileymiller.dev/metrics', siteName: 'MusicShare' });
    expect(image).toMatchObject({ width: 1200, height: 630, type: 'image/png', alt: 'MusicShare metrics: 289 songs and 6 added in the last 7 days.' });
    expect(image?.url).toBe('https://music.baileymiller.dev/metrics/share-image?v=2026-08-31T12%3A00%3A00.000Z-289-289-268-6');
    expect(metadata.twitter).toMatchObject({ card: 'summary_large_image', images: [image?.url] });
  });

  it('uses generic metadata without fallback zero claims when metrics are unavailable', async () => {
    fetchMock.mockRejectedValue(new Error('offline'));
    const metadata = await generateMetadata();
    expect(metadata.description).toBe('Explore public MusicShare activity and resolved platform links.');
    const openGraphImages = metadata.openGraph?.images;
    const image = (Array.isArray(openGraphImages) ? openGraphImages[0] : openGraphImages) as { url?: string };
    expect(image).toMatchObject({ url: 'https://music.baileymiller.dev/metrics/share-image?v=unavailable' });
    expect(JSON.stringify(metadata)).not.toContain('0 songs');
  });
});
