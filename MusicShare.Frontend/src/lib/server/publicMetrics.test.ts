import { describe, expect, it, vi } from 'vitest';
import { MusicServiceType } from '../api';
import { getPublicMetrics, metricsShareCopy, previewVersion, resolvePublicMetricsApiOrigin, summarizePublicMetrics } from './publicMetrics';

const snapshot = {
  totalCompletedSongs: 4,
  generatedAt: '2026-08-31T12:00:00Z',
  serviceCounts: [{ service: MusicServiceType.Spotify, count: 3 }, { service: MusicServiceType.YouTubeMusic, count: 2 }],
  recentSongs: [],
  weeklyCompletedSongs: [{ weekStart: '2026-08-30T00:00:00Z', count: 1 }],
};

describe('public metrics boundary', () => {
  it('prefers the private HTTPS service reference and recognizes valid zero snapshots', async () => {
    const fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ ...snapshot, totalCompletedSongs: 0, serviceCounts: [], weeklyCompletedSongs: [] }) });
    const result = await getPublicMetrics({ env: { services__api__https__0: 'https://api.internal', services__api__http__0: 'http://api.internal' }, fetch });
    expect(fetch).toHaveBeenCalledWith('https://api.internal/api/metrics');
    expect(result).toMatchObject({ available: true, metrics: { totalCompletedSongs: 0 } });
  });

  it.each([
    [{ ok: false }],
    [{ ok: true, json: async () => ({ ...snapshot, serviceCounts: [{ service: 2, count: 1 }] }) }],
    [{ ok: true, json: async () => ({ ...snapshot, weeklyCompletedSongs: [{ weekStart: 'invalid', count: 1 }] }) }],
  ])('contains invalid responses as an unavailable snapshot', async (response) => {
    const result = await getPublicMetrics({ fetch: vi.fn().mockResolvedValue(response) });
    expect(result).toMatchObject({ available: false, metrics: { totalCompletedSongs: 0 } });
  });

  it('contains JSON and network failures', async () => {
    await expect(getPublicMetrics({ fetch: vi.fn().mockRejectedValue(new Error('offline')) })).resolves.toMatchObject({ available: false });
    await expect(getPublicMetrics({ fetch: vi.fn().mockResolvedValue({ ok: true, json: async () => { throw new Error('invalid JSON'); } }) })).resolves.toMatchObject({ available: false });
  });

  it('derives factual sharing values and changes its version when displayed data changes', () => {
    expect(summarizePublicMetrics(snapshot)).toEqual({ completedSongs: 4, spotifyLinks: 3, youTubeMusicLinks: 2, thisWeekCompletedSongs: 1 });
    expect(previewVersion(snapshot)).not.toBe(previewVersion({ ...snapshot, totalCompletedSongs: 5 }));
    expect(metricsShareCopy({ available: true, metrics: snapshot }).description).toContain('4 completed songs');
    expect(metricsShareCopy({ available: false, metrics: snapshot }).description).not.toMatch(/\d/);
    expect(resolvePublicMetricsApiOrigin({ services__api__http__0: 'http://api.internal' })).toBe('http://api.internal');
  });
});
