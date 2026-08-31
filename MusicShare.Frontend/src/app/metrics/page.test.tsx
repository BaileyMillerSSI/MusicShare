import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MusicServiceType } from '../../lib/api';

const fetchMock = vi.fn();
vi.stubGlobal('fetch', fetchMock);
const { default: MetricsPage } = await import('./page');

afterEach(() => vi.clearAllMocks());

describe('MetricsPage', () => {
  it('renders known service counts and canonical share links', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ totalCompletedSongs: 1, serviceCounts: [
      { service: MusicServiceType.Spotify, count: 1 }, { service: MusicServiceType.AppleMusic, count: 0 }, { service: MusicServiceType.YouTubeMusic, count: 0 },
    ], recentSongs: [{ songId: 'song-1', shareId: 'abc123def456', title: 'Song', artists: ['Artist'], sourceService: MusicServiceType.Spotify, createdAt: '2026-01-01T00:00:00Z' }] }) });
    render(await MetricsPage());
    expect(screen.getByText('Spotify')).toBeInTheDocument();
    expect(screen.getByText('Apple Music')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /song/i })).toHaveAttribute('href', '/share/abc123def456');
  });

  it('renders a safe empty state when the internal API is unavailable', async () => {
    fetchMock.mockRejectedValue(new Error('offline'));
    render(await MetricsPage());
    expect(screen.getByText(/no completed songs yet/i)).toBeInTheDocument();
  });
});
