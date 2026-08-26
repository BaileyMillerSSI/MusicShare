import type { ShareResultResponse } from '../../../../lib/api';

const ARTWORK_TIMEOUT_MS = 10000;

function getApiBase(): string {
  return (
    process.env.services__api__https__0 ??
    process.env.services__api__http__0 ??
    'http://localhost:5222'
  );
}

async function fetchWithTimeout(url: string): Promise<Response> {
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), ARTWORK_TIMEOUT_MS);

  try {
    return await fetch(url, { signal: controller.signal });
  } finally {
    clearTimeout(timeoutId);
  }
}

export async function GET(
  _request: Request,
  { params }: { params: Promise<{ shareId: string }> }
): Promise<Response> {
  const { shareId } = await params;

  try {
    const resultResponse = await fetchWithTimeout(
      `${getApiBase()}/api/share/${encodeURIComponent(shareId)}`
    );
    if (!resultResponse.ok) return new Response(null, { status: 404 });

    const result = (await resultResponse.json()) as ShareResultResponse;
    if (!result.song?.artworkUrl) return new Response(null, { status: 404 });

    let artworkUrl: URL;
    try {
      artworkUrl = new URL(result.song.artworkUrl);
    } catch {
      return new Response(null, { status: 400 });
    }

    if (!['http:', 'https:'].includes(artworkUrl.protocol)) {
      return new Response(null, { status: 400 });
    }

    const artworkResponse = await fetchWithTimeout(artworkUrl.toString());
    if (!artworkResponse.ok || !artworkResponse.body) {
      return new Response(null, { status: 404 });
    }

    const contentType = artworkResponse.headers.get('content-type')?.split(';', 1)[0];
    if (!contentType?.startsWith('image/')) {
      return new Response(null, { status: 415 });
    }

    return new Response(artworkResponse.body, {
      headers: {
        'Cache-Control': 'public, max-age=3600, s-maxage=86400, stale-while-revalidate=604800',
        'Content-Type': contentType,
      },
    });
  } catch {
    return new Response(null, { status: 502 });
  }
}
