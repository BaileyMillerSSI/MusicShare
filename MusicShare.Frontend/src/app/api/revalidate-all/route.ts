import { revalidatePath } from 'next/cache';
import { NextResponse } from 'next/server';

interface GetShareIdsResponse {
  shareIds: string[];
}

export async function POST(request: Request) {
  const secret = process.env.REVALIDATION_SECRET;

  if (!secret) {
    return NextResponse.json({ error: 'Revalidation secret not configured' }, { status: 500 });
  }

  const authorization = request.headers.get('X-API-KEY');
  if (authorization !== secret) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const apiBase =
    process.env.services__api__https__0 ??
    process.env.services__api__http__0;

  if (!apiBase) {
    return NextResponse.json({ error: 'API base URL not configured' }, { status: 500 });
  }

  const res = await fetch(`${apiBase}/internal/share/ids`);
  if (!res.ok) {
    return NextResponse.json({ error: 'Failed to fetch share IDs from API' }, { status: 502 });
  }

  const data: GetShareIdsResponse = await res.json();
  const shareIds = data.shareIds ?? [];

  for (const shareId of shareIds) {
    revalidatePath(`/share/${shareId}`);
  }

  return NextResponse.json({ revalidated: true, count: shareIds.length, shareIds });
}
