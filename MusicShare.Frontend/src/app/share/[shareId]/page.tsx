import type { Metadata } from 'next';
import { type ShareResultResponse, api } from '../../../lib/api';
import { ResultPoller } from '../../../components/ResultPoller';

export const revalidate = false; // cache indefinitely; revalidated on-demand by the Worker

interface PageProps {
  params: Promise<{ shareId: string }>;
}

export async function generateStaticParams() {
  // Pre-cache all completed share routes at build time
  const apiBase =
    process.env.services__api__https__0 ??
    process.env.services__api__http__0 ??
    undefined;

  try {
    const shareIds = await api.getAllShareIds(apiBase);
    return shareIds.map((shareId) => ({ shareId }));
  } catch {
    // Gracefully handle API unavailability during build
    return [];
  }
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

    if (!data.song) {
      return {
        title: 'Share Result',
      };
    }

    const artistsString = data.song.artists.join(', ');
    const title = `${data.song.title} - ${artistsString}`;
    const description = `Listen to ${data.song.title} from ${artistsString} across multiple platforms`;

    return {
      title,
      description,
      openGraph: {
        title,
        description,
        type: 'music.song',
        images: data.song.artworkUrl
          ? [{ url: data.song.artworkUrl, width: 300, height: 300, alt: title }]
          : [],
      },
      twitter: {
        card: 'summary_large_image',
        title,
        description,
        images: data.song.artworkUrl ? [data.song.artworkUrl] : [],
      },
    };
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

  return (
    <div className="min-h-screen bg-linear-to-br from-purple-500 to-pink-500 flex items-center justify-center p-4">
      <div className="bg-white rounded-lg shadow-xl p-8 max-w-2xl w-full">
        {/* Pass initialData only when Completed; otherwise client polls fresh */}
        <ResultPoller
          shareId={shareId}
          initialData={data.status === 'Completed' ? data : undefined}
        />
      </div>
    </div>
  );
}
