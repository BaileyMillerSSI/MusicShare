import { NextResponse } from 'next/server';

export async function POST(request: Request) {
  const secret = process.env.REINDEX_API_KEY;
  if (!secret) {
    return NextResponse.json({ error: 'Re-index API key not configured' }, { status: 500 });
  }

  const apiKey = request.headers.get('X-API-KEY');
  if (apiKey !== secret) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const apiUrl = process.env.API_URL;
  const response = await fetch(`${apiUrl}/internal/reindex/all`, {
    method: 'POST',
    headers: { 'X-API-KEY': secret },
  });

  const body = await response.json();
  return NextResponse.json(body, { status: response.status });
}
