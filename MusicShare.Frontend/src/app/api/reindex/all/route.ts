import { NextResponse } from 'next/server';
import { parseApiBaseUrl, validateApiKey } from '../../_lib/maintenance';

export async function POST(request: Request) {
  const secret = process.env.REINDEX_API_KEY;
  const authError = validateApiKey(request, secret, 'Re-index API key');
  if (authError) {
    return authError;
  }

  const apiUrl = parseApiBaseUrl(process.env.API_URL);
  if (!apiUrl) {
    return NextResponse.json({ error: 'API_URL not configured or invalid' }, { status: 500 });
  }

  const response = await fetch(new URL('/internal/reindex/all', apiUrl), {
    method: 'POST',
    headers: { 'X-API-KEY': secret },
  });

  const body = await response.json();
  return NextResponse.json(body, { status: response.status });
}
