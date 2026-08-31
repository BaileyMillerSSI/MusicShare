import type { Metadata } from 'next';
import Link from 'next/link';
import { BreadstickFooter } from '../../components/BreadstickFooter';
import { MusicServiceType, type PublicMetricsResponse } from '../../lib/api';

export const revalidate = false;
export const metadata: Metadata = { title: 'Music metrics', description: 'Recently shared music and source platform counts.' };

const services = [MusicServiceType.Spotify, MusicServiceType.AppleMusic, MusicServiceType.YouTubeMusic];
const labels: Record<number, string> = { [MusicServiceType.Spotify]: 'Spotify', [MusicServiceType.AppleMusic]: 'Apple Music', [MusicServiceType.YouTubeMusic]: 'YouTube Music' };

function emptyMetrics(): PublicMetricsResponse {
  return { totalCompletedSongs: 0, serviceCounts: services.map((service) => ({ service, count: 0 })), recentSongs: [] };
}

async function getMetrics(): Promise<PublicMetricsResponse> {
  const apiBase = process.env.services__api__https__0 ?? process.env.services__api__http__0 ?? '.';
  try {
    const response = await fetch(`${apiBase}/api/metrics`);
    if (!response.ok) return emptyMetrics();
    const data: unknown = await response.json();
    if (!data || typeof data !== 'object' || !Array.isArray((data as PublicMetricsResponse).serviceCounts) || !Array.isArray((data as PublicMetricsResponse).recentSongs)) return emptyMetrics();
    return data as PublicMetricsResponse;
  } catch { return emptyMetrics(); }
}

export default async function MetricsPage() {
  const metrics = await getMetrics();
  const counts = new Map(metrics.serviceCounts.map((item) => [item.service, item.count]));
  const recentSongs = metrics.recentSongs.slice(0, 20);
  return <div className="min-h-screen bg-linear-to-br from-purple-500 to-pink-500 flex flex-col items-center gap-4 p-4 py-10">
    <main className="bg-white rounded-lg shadow-xl p-8 max-w-3xl w-full">
      <Link className="text-sm text-purple-700 hover:underline" href="/">← Share music</Link>
      <h1 className="mt-4 text-3xl font-bold text-gray-800">Music metrics</h1>
      <p className="mt-2 text-gray-600">{metrics.totalCompletedSongs} completed songs shared across Music Share.</p>
      <section className="mt-6 grid grid-cols-1 sm:grid-cols-3 gap-3" aria-label="Source platform counts">
        {services.map((service) => <div key={service} className="rounded-lg bg-gray-100 p-4"><p className="text-sm text-gray-600">{labels[service]}</p><p className="text-2xl font-bold text-gray-800">{counts.get(service) ?? 0}</p></div>)}
      </section>
      <section className="mt-8" aria-labelledby="recent-songs"><h2 id="recent-songs" className="text-xl font-semibold text-gray-800">Recently added</h2>
        {recentSongs.length === 0 ? <p className="mt-3 text-gray-600">No completed songs yet.</p> : <ul className="mt-3 divide-y divide-gray-200">
          {recentSongs.map((song) => <li key={song.songId} className="py-3"><Link className="flex items-center gap-3 hover:text-purple-700" href={`/share/${encodeURIComponent(song.shareId)}`}>
            {song.artworkUrl ? <img src={song.artworkUrl} alt="" className="h-12 w-12 rounded object-cover" /> : null}
            <span><span className="block font-medium">{song.title}</span><span className="block text-sm text-gray-600">{song.artists.join(', ')} · {labels[song.sourceService]}</span></span>
          </Link></li>)}
        </ul>}
      </section>
    </main><BreadstickFooter />
  </div>;
}
