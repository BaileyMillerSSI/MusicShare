import type { Metadata } from 'next';
import { type ShareResultResponse } from '../../../lib/api';
import { durationToSeconds } from '../../../lib/utils';
import { BreadstickFooter } from '../../../components/BreadstickFooter';
import { ResultPoller } from '../../../components/ResultPoller';
import { permanentRedirect } from 'next/navigation';

export const revalidate = false; // cache indefinitely; revalidated on-demand by the Worker

interface PageProps {
  params: Promise<{ shareId: string }>;
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { shareId } = await params;

  const apiBase =
    process.env.services__api__https__0 ??
    process.env.services__api__http__0 ??
    '.';

  try {
    const res = await fetch(`${apiBase}/api/share/${shareId}`);
    const data: ShareResultResponse = await res.json();
    // Metadata must use the same fail-closed canonical-ID rule as the page; never
    // emit an arbitrary backend value as a public canonical URL.
    const canonicalShareId = /^[a-f0-9]{12}$/.test(data.shareId) ? data.shareId : shareId;

    if (!data.song) {
      return {
        title: 'Share Result',
      };
    }

    const artistsString = data.song.artists.join(', ');
    const title = `${data.song.title} - ${artistsString}`;
    const description = `Listen to ${data.song.title} from ${artistsString} across multiple platforms`;
    const durationSeconds = durationToSeconds(data.song.duration);

    const metadata: Metadata = {
      title,
      description,
      openGraph: {
        title,
        description,
        type: 'music.song',
        url: `/share/${canonicalShareId}`,
        images: data.song.artworkUrl
          ? [{ url: data.song.artworkUrl, width: 300, height: 300, alt: title }]
          : [],
      },
      alternates: { canonical: `/share/${canonicalShareId}` },
      twitter: {
        card: 'summary_large_image',
        title,
        description,
        images: data.song.artworkUrl ? [data.song.artworkUrl] : [],
      },
    };

    // Add music:duration meta tag if duration is available
    if (durationSeconds !== null) {
      metadata.other = {
        'music:duration': durationSeconds.toString(),
      };
    }

    return metadata;
  } catch {
    return {
      title: 'Share Result',
    };
  }
}

export default async function ShareResultPage({ params }: PageProps) {
  const { shareId } = await params;

  // Absolute URL — relative paths don't resolve in server-side fetch
  const apiBase =
    process.env.services__api__https__0 ??
    process.env.services__api__http__0 ??
    '.';

  const res = await fetch(`${apiBase}/api/share/${shareId}`);
  const data: ShareResultResponse = await res.json();
  const canonicalShareId = /^[a-f0-9]{12}$/.test(data.shareId) ? data.shareId : shareId;
  if (canonicalShareId !== shareId) {
    permanentRedirect(`/share/${canonicalShareId}`);
  }

  return (
    <div className="min-h-screen bg-linear-to-br from-purple-500 to-pink-500 flex flex-col items-center justify-center gap-4 p-4">
      <div className="bg-white rounded-lg shadow-xl p-8 max-w-2xl w-full">
        {/* Pass initialData only when Completed; otherwise client polls fresh */}
        <ResultPoller
          shareId={canonicalShareId}
          initialData={data.status === 'Completed' ? data : undefined}
        />
      </div>
      <BreadstickFooter />
    </div>
  );
}
