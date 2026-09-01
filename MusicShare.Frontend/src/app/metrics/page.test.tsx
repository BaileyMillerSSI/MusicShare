import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MusicServiceType } from '../../lib/api';

const fetchMock = vi.fn();
vi.stubGlobal('fetch', fetchMock);
const { default: MetricsPage, dynamic, metadata } = await import('./page');

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
    expect(screen.getByText('Completed songs')).toBeInTheDocument();
    expect(screen.getByText('Spotify links')).toBeInTheDocument();
    expect(screen.getByText('YouTube Music links')).toBeInTheDocument();
    expect(screen.queryByText('Apple Music')).not.toBeInTheDocument();
    expect(screen.queryByText(/source platform/i)).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: /song/i })).toHaveAttribute('href', '/share/abc123def456');
  });

  it('renders the current weekly change and simple weekly chart including zeroes', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ totalCompletedSongs: 6, serviceCounts: [], recentSongs: [], weeklyCompletedSongs: [
      { weekStart: '2026-01-04T00:00:00Z', count: 0 }, { weekStart: '2026-01-11T00:00:00Z', count: 2 },
      { weekStart: '2026-01-18T00:00:00Z', count: 0 }, { weekStart: '2026-01-25T00:00:00Z', count: 1 },
      { weekStart: '2026-02-01T00:00:00Z', count: 0 }, { weekStart: '2026-02-08T00:00:00Z', count: 0 },
      { weekStart: '2026-02-15T00:00:00Z', count: 0 }, { weekStart: '2026-02-22T00:00:00Z', count: 3 },
    ] }) });
    render(await MetricsPage());

    expect(screen.getByText('+3 this week')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Completed songs by week' })).toBeInTheDocument();
    expect(screen.getByLabelText('2026-01-04 UTC: 0 completed songs')).toBeInTheDocument();
    expect(screen.getByLabelText('2026-02-22 UTC: 3 completed songs')).toBeInTheDocument();
    expect(screen.getByLabelText('2026-01-04 UTC: 0 completed songs').querySelector('[aria-hidden="true"]')).toHaveStyle({ height: '0%' });
    expect(screen.getByLabelText('2026-01-11 UTC: 2 completed songs').querySelector('[aria-hidden="true"]')).toHaveStyle({ height: '67%' });
  });

  it('supports snapshots created before weekly metrics were stored', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ totalCompletedSongs: 1, serviceCounts: [], recentSongs: [] }) });
    render(await MetricsPage());
    expect(screen.getByText('+0 this week')).toBeInTheDocument();
    expect(screen.getByText(/weekly song data is not available yet/i)).toBeInTheDocument();
  });

  it('renders a safe empty state when the internal API is unavailable', async () => {
    fetchMock.mockRejectedValue(new Error('offline'));
    render(await MetricsPage());
    expect(screen.getByText(/no completed songs yet/i)).toBeInTheDocument();
  });

  it('preserves backend newest-first order and caps the rendered list at 20', async () => {
    const recentSongs = Array.from({ length: 21 }, (_, index) => ({ songId: `song-${index}`, shareId: `share-${index}`, title: `Song ${index}`, artists: ['Artist'], sourceService: MusicServiceType.Spotify, createdAt: '2026-01-01T00:00:00Z' }));
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ totalCompletedSongs: 21, serviceCounts: [], recentSongs }) });

    render(await MetricsPage());

    expect(screen.getAllByRole('link', { name: /song/i }).filter((link) => link.getAttribute('href')?.startsWith('/share/'))).toHaveLength(20);
    expect(screen.getByRole('link', { name: /song 0/i })).toHaveAttribute('href', '/share/share-0');
    expect(screen.queryByRole('link', { name: /song 20/i })).not.toBeInTheDocument();
  });

  it('renders an all-zero weekly chart without errors', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ totalCompletedSongs: 0, serviceCounts: [], recentSongs: [], weeklyCompletedSongs: [
      { weekStart: '2026-01-04T00:00:00Z', count: 0 }, { weekStart: '2026-01-11T00:00:00Z', count: 0 },
    ] }) });
    render(await MetricsPage());

    expect(screen.getAllByText('0')).toHaveLength(5);
    expect(screen.getByLabelText('2026-01-04 UTC: 0 completed songs')).toBeInTheDocument();
    expect(metadata.title).toBe('Music metrics');
  });

  it.each([
    { totalCompletedSongs: 1, serviceCounts: 'invalid', recentSongs: [] },
    { totalCompletedSongs: 1, serviceCounts: [], recentSongs: [{ songId: 3 }] },
    { totalCompletedSongs: 1, serviceCounts: [], recentSongs: [], weeklyCompletedSongs: [{ weekStart: 'not-a-date', count: 1 }] },
  ])('renders a safe empty state for malformed successful payloads', async (payload) => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => payload });
    render(await MetricsPage());
    expect(screen.getByText(/no completed songs yet/i)).toBeInTheDocument();
  });

  it('renders a safe empty state for unsuccessful responses', async () => {
    fetchMock.mockResolvedValue({ ok: false });
    render(await MetricsPage());
    expect(screen.getByText(/no completed songs yet/i)).toBeInTheDocument();
  });
});
