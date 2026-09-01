import { MusicServiceType, type PublicMetricsResponse } from '../api';

const services = [MusicServiceType.Spotify, MusicServiceType.YouTubeMusic] as const;

export const publicMetricsOrigin = 'https://music.baileymiller.dev';

export type PublicMetricsFetchResult = {
  available: boolean;
  metrics: PublicMetricsResponse;
};

export type PublicMetricsSummary = {
  completedSongs: number;
  spotifyLinks: number;
  youTubeMusicLinks: number;
  lastSevenDaysCompletedSongs: number;
};

type MetricsDependencies = {
  env?: Readonly<Record<string, string | undefined>>;
  fetch?: typeof fetch;
};

export function emptyPublicMetrics(): PublicMetricsResponse {
  return {
    totalCompletedSongs: 0,
    serviceCounts: services.map((service) => ({ service, count: 0 })),
    recentSongs: [],
    dailyCompletedSongs: [],
  };
}

export function resolvePublicMetricsApiOrigin(env: Readonly<Record<string, string | undefined>> = process.env): string {
  return env.services__api__https__0 ?? env.services__api__http__0 ?? '.';
}

export function isPublicMetricsResponse(value: unknown): value is PublicMetricsResponse {
  if (!value || typeof value !== 'object') return false;

  const metrics = value as Partial<PublicMetricsResponse>;
  if (!Number.isFinite(metrics.totalCompletedSongs) || metrics.totalCompletedSongs! < 0
    || !Array.isArray(metrics.serviceCounts) || !Array.isArray(metrics.recentSongs)
    || (metrics.generatedAt !== undefined && (typeof metrics.generatedAt !== 'string' || Number.isNaN(Date.parse(metrics.generatedAt))))) return false;

  return metrics.serviceCounts.every((count) => services.includes(count.service as typeof services[number])
    && Number.isFinite(count.count) && count.count >= 0)
    && metrics.recentSongs.every((song) => typeof song.songId === 'string' && typeof song.shareId === 'string'
      && typeof song.title === 'string' && Array.isArray(song.artists) && song.artists.every((artist) => typeof artist === 'string')
      && typeof song.createdAt === 'string')
    && (metrics.dailyCompletedSongs === undefined || (Array.isArray(metrics.dailyCompletedSongs)
      && (metrics.dailyCompletedSongs.length === 0 || metrics.dailyCompletedSongs.length === 7)
      && metrics.dailyCompletedSongs.every((week) => typeof week.dayStart === 'string' && !Number.isNaN(Date.parse(week.dayStart))
        && Number.isFinite(week.count) && week.count >= 0)));
}

export async function getPublicMetrics(dependencies: MetricsDependencies = {}): Promise<PublicMetricsFetchResult> {
  const fetcher = dependencies.fetch ?? fetch;
  const apiOrigin = resolvePublicMetricsApiOrigin(dependencies.env);

  try {
    const response = await fetcher(`${apiOrigin}/api/metrics`);
    if (!response.ok) return { available: false, metrics: emptyPublicMetrics() };

    const data: unknown = await response.json();
    return isPublicMetricsResponse(data)
      ? { available: true, metrics: data }
      : { available: false, metrics: emptyPublicMetrics() };
  } catch {
    return { available: false, metrics: emptyPublicMetrics() };
  }
}

export function summarizePublicMetrics(metrics: PublicMetricsResponse): PublicMetricsSummary {
  const counts = new Map(metrics.serviceCounts.map((item) => [item.service, item.count]));
  return {
    completedSongs: metrics.totalCompletedSongs,
    spotifyLinks: counts.get(MusicServiceType.Spotify) ?? 0,
    youTubeMusicLinks: counts.get(MusicServiceType.YouTubeMusic) ?? 0,
    lastSevenDaysCompletedSongs: metrics.dailyCompletedSongs?.reduce((total, day) => total + day.count, 0) ?? 0,
  };
}

export function previewVersion(metrics: PublicMetricsResponse): string {
  const summary = summarizePublicMetrics(metrics);
  const generatedAt = metrics.generatedAt && !Number.isNaN(Date.parse(metrics.generatedAt)) ? new Date(metrics.generatedAt).toISOString() : 'snapshot';
  return `${generatedAt}-${summary.completedSongs}-${summary.spotifyLinks}-${summary.youTubeMusicLinks}-${summary.lastSevenDaysCompletedSongs}`;
}

export function metricsShareCopy(result: PublicMetricsFetchResult): { title: string; description: string; imageAlt: string } {
  if (!result.available) {
    return {
      title: 'Music metrics | MusicShare',
      description: 'Explore public MusicShare activity and resolved platform links.',
      imageAlt: 'MusicShare metrics',
    };
  }

  const summary = summarizePublicMetrics(result.metrics);
  return {
    title: 'Music metrics | MusicShare',
    description: `${summary.completedSongs} songs, ${summary.spotifyLinks} Spotify links, ${summary.youTubeMusicLinks} YouTube Music links, and ${summary.lastSevenDaysCompletedSongs} added in the last 7 days.`,
    imageAlt: `MusicShare metrics: ${summary.completedSongs} songs and ${summary.lastSevenDaysCompletedSongs} added in the last 7 days.`,
  };
}
