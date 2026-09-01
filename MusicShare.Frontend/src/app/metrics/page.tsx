import type { Metadata } from 'next';
import Link from 'next/link';
import { BreadstickFooter } from '../../components/BreadstickFooter';
import { MusicServiceType } from '../../lib/api';
import { getPublicMetrics, metricsShareCopy, previewVersion, publicMetricsOrigin, summarizePublicMetrics } from '../../lib/server/publicMetrics';

export const dynamic = 'force-dynamic';
const services = [MusicServiceType.Spotify, MusicServiceType.YouTubeMusic];
const labels: Record<number, string> = { [MusicServiceType.Spotify]: 'Spotify links', [MusicServiceType.YouTubeMusic]: 'YouTube Music links' };

export async function generateMetadata(): Promise<Metadata> {
  const result = await getPublicMetrics();
  const copy = metricsShareCopy(result);
  const imageUrl = new URL('/metrics/share-image', publicMetricsOrigin);
  imageUrl.searchParams.set('v', result.available ? previewVersion(result.metrics) : 'unavailable');
  const image = { url: imageUrl.toString(), width: 1200, height: 630, alt: copy.imageAlt, type: 'image/png' as const };

  return {
    title: copy.title,
    description: copy.description,
    alternates: { canonical: `${publicMetricsOrigin}/metrics` },
    openGraph: { type: 'website', url: `${publicMetricsOrigin}/metrics`, siteName: 'MusicShare', title: copy.title, description: copy.description, images: [image] },
    twitter: { card: 'summary_large_image', title: copy.title, description: copy.description, images: [imageUrl.toString()] },
  };
}

export default async function MetricsPage() {
  const { metrics } = await getPublicMetrics();
  const summary = summarizePublicMetrics(metrics);
  const recentSongs = metrics.recentSongs.slice(0, 20);
  const weeklyCompletedSongs = metrics.weeklyCompletedSongs ?? [];
  const thisWeekCount = summary.thisWeekCompletedSongs;
  const largestWeeklyCount = Math.max(0, ...weeklyCompletedSongs.map((week) => week.count));
  return <div className="min-h-screen bg-linear-to-br from-purple-500 to-pink-500 flex flex-col items-center gap-4 p-4 py-10">
    <main className="bg-white rounded-lg shadow-xl p-8 max-w-3xl w-full">
      <Link className="text-sm text-purple-700 hover:underline" href="/">← Share music</Link>
      <h1 className="mt-4 text-3xl font-bold text-gray-800">Music metrics</h1>
      <p className="mt-2 text-gray-600">{metrics.totalCompletedSongs} completed songs shared across Music Share.</p>
      <section className="mt-6 grid grid-cols-1 sm:grid-cols-3 gap-3" aria-label="Resolved platform link counts">
        <div className="rounded-lg bg-gray-100 p-4"><p className="text-sm text-gray-600">Completed songs</p><p className="text-2xl font-bold text-gray-800">{metrics.totalCompletedSongs}</p><p className="mt-1 text-sm font-medium text-purple-700">+{thisWeekCount} this week</p></div>
        {services.map((service) => <div key={service} className="rounded-lg bg-gray-100 p-4"><p className="text-sm text-gray-600">{labels[service]}</p><p className="text-2xl font-bold text-gray-800">{service === MusicServiceType.Spotify ? summary.spotifyLinks : summary.youTubeMusicLinks}</p></div>)}
      </section>
      <section className="mt-8" aria-labelledby="weekly-completed-songs">
        <h2 id="weekly-completed-songs" className="text-xl font-semibold text-gray-800">Completed songs by week</h2>
        {weeklyCompletedSongs.length === 0 ? <p className="mt-3 text-gray-600">Weekly song data is not available yet.</p> : <ol className="mt-3 grid grid-cols-8 gap-1.5" aria-label="Completed songs by week, Sunday UTC start">
          {weeklyCompletedSongs.map((week) => {
            const height = week.count === 0 || largestWeeklyCount === 0 ? 0 : Math.max(8, Math.round((week.count / largestWeeklyCount) * 100));
            const label = `${week.weekStart.slice(0, 10)} UTC: ${week.count} completed songs`;
            return <li key={week.weekStart} className="min-w-0 text-center text-xs text-gray-600" aria-label={label}>
              <span className="flex h-28 items-end justify-center rounded bg-gray-100 px-0.5"><span className="w-full rounded-t bg-purple-600" style={{ height: `${height}%` }} aria-hidden="true" /></span>
              <span className="mt-1 block font-medium text-gray-800">{week.count}</span>
              <time className="block text-[10px] leading-tight" dateTime={week.weekStart}>{week.weekStart.slice(5, 10)} UTC</time>
            </li>;
          })}
        </ol>}
      </section>
      <section className="mt-8" aria-labelledby="recent-songs"><h2 id="recent-songs" className="text-xl font-semibold text-gray-800">Recently added</h2>
        {recentSongs.length === 0 ? <p className="mt-3 text-gray-600">No completed songs yet.</p> : <ul className="mt-3 divide-y divide-gray-200">
          {recentSongs.map((song) => <li key={song.songId} className="py-3"><Link className="flex items-center gap-3 hover:text-purple-700" href={`/share/${encodeURIComponent(song.shareId)}`}>
            {song.artworkUrl ? <img src={song.artworkUrl} alt="" className="h-12 w-12 rounded object-cover" /> : null}
            <span><span className="block font-medium">{song.title}</span><span className="block text-sm text-gray-600">{song.artists.join(', ')}</span></span>
          </Link></li>)}
        </ul>}
      </section>
    </main><BreadstickFooter />
  </div>;
}
