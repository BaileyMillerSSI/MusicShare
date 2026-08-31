import { revalidatePath } from 'next/cache';
import { NextResponse } from 'next/server';

const shareIdPattern = /^[a-f0-9]{12}$/;

function isRequestBody(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
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

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ error: 'Invalid JSON body' }, { status: 400 });
  }

  if (!isRequestBody(body)) {
    return NextResponse.json({ error: 'Invalid revalidation target' }, { status: 400 });
  }

  if (body.target === 'metrics' && Object.keys(body).length === 1) {
    revalidatePath('/metrics');
    return NextResponse.json({ revalidated: true, target: 'metrics' });
  }

  if (Object.keys(body).length !== 1 || typeof body.shareId !== 'string') {
    return NextResponse.json({ error: 'Invalid revalidation target' }, { status: 400 });
  }

  const shareId = body.shareId;

  if (!shareIdPattern.test(shareId)) {
    return NextResponse.json(
      { error: 'shareId must be a 12-character hexadecimal string' },
      { status: 400 }
    );
  }

  const sharePath = `/share/${encodeURIComponent(shareId)}`;

  console.log(`Starting revalidation for shareId: ${shareId}`);
  revalidatePath(sharePath);
  console.log(`Finished revalidation for shareId: ${shareId}`);

  return NextResponse.json({ revalidated: true, shareId });
}
