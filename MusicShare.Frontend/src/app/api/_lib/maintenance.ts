import { NextResponse } from 'next/server';

const shareIdPattern = /^[a-f0-9]{12}$/i;
const songIdPattern = /^[a-f0-9]{24}$/i;

export function validateApiKey(request: Request, secret: string | undefined, secretName: string) {
  if (!secret) {
    return NextResponse.json({ error: `${secretName} not configured` }, { status: 500 });
  }

  const apiKey = request.headers.get('X-API-KEY');
  if (apiKey !== secret) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  return null;
}

export function isValidShareId(shareId: unknown): shareId is string {
  return typeof shareId === 'string' && shareIdPattern.test(shareId);
}

export function isValidSongId(songId: unknown): songId is string {
  return typeof songId === 'string' && songIdPattern.test(songId);
}

export function getSharePath(shareId: string) {
  return `/share/${encodeURIComponent(shareId)}`;
}

export function parseApiBaseUrl(rawUrl: string | undefined) {
  if (!rawUrl) {
    return null;
  }

  try {
    const url = new URL(rawUrl);
    if (url.protocol !== 'http:' && url.protocol !== 'https:') {
      return null;
    }

    return url;
  } catch {
    return null;
  }
}
