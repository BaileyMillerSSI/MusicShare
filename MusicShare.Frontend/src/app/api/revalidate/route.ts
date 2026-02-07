import { revalidatePath } from 'next/cache';
import { NextResponse } from 'next/server';

export async function POST(request: Request) {
  const secret = process.env.REVALIDATION_SECRET;

  if (!secret) {
    return NextResponse.json({ error: 'Revalidation secret not configured' }, { status: 500 });
  }

  const authorization = request.headers.get('X-API-KEY');
  if (authorization !== secret) {
    return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  }

  const { shareId } = (await request.json()) as { shareId?: string };
  if (!shareId) {
    return NextResponse.json({ error: 'shareId is required' }, { status: 400 });
  }

  console.log(`Starting revalidation for shareId: ${shareId}`);
  revalidatePath(`/share/${shareId}`);
  console.log(`Finished revalidation for shareId: ${shareId}`);

  return NextResponse.json({ revalidated: true, shareId });
}
