import { revalidatePath } from 'next/cache';
import { NextResponse } from 'next/server';
import { getSharePath, isValidShareId, parseApiBaseUrl, validateApiKey } from '../_lib/maintenance';

interface GetShareIdsResponse {
  shareIds: string[];
}

export async function POST(request: Request) {
  const secret = process.env.REVALIDATION_SECRET;

  const authError = validateApiKey(request, secret, 'Revalidation secret');
  if (authError) {
    return authError;
  }

  const apiBase =
    process.env.services__api__https__0 ??
    process.env.services__api__http__0;
  const apiBaseUrl = parseApiBaseUrl(apiBase);

  if (!apiBaseUrl) {
    return NextResponse.json({ error: 'API base URL not configured or invalid' }, { status: 500 });
  }

  const res = await fetch(new URL('/internal/share/ids', apiBaseUrl));
  if (!res.ok) {
    return NextResponse.json({ error: 'Failed to fetch share IDs from API' }, { status: 502 });
  }

  const data: GetShareIdsResponse = await res.json();
  const shareIds = data.shareIds ?? [];
  const validShareIds = shareIds.filter(isValidShareId);

  if (validShareIds.length !== shareIds.length) {
    return NextResponse.json({ error: 'API returned malformed share IDs' }, { status: 502 });
  }

  for (const shareId of validShareIds) {
    revalidatePath(getSharePath(shareId));
  }

  return NextResponse.json({ revalidated: true, count: validShareIds.length });
}
