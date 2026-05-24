import { NextResponse } from 'next/server';
import { isValidSongId, parseApiBaseUrl, validateApiKey } from '../../../_lib/maintenance';

export async function POST(
  request: Request,
  { params }: { params: Promise<{ songId: string }> }
) {
  const secret = process.env.REINDEX_API_KEY;
  const authError = validateApiKey(request, secret, 'Re-index API key');
  if (authError) {
    return authError;
  }

  const { songId } = await params;
  if (!isValidSongId(songId)) {
    return NextResponse.json({ error: 'songId must be a 24-character hexadecimal ObjectId' }, { status: 400 });
  }

  const apiUrl = parseApiBaseUrl(process.env.API_URL);
  if (!apiUrl) {
    return NextResponse.json({ error: 'API_URL not configured or invalid' }, { status: 500 });
  }

  const response = await fetch(new URL(`/internal/reindex/song/${encodeURIComponent(songId)}`, apiUrl), {
    method: 'POST',
    headers: { 'X-API-KEY': secret },
  });

  const body = await response.json();
  return NextResponse.json(body, { status: response.status });
}
