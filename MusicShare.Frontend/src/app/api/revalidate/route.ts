import { revalidatePath } from 'next/cache';
import { NextResponse } from 'next/server';
import { getSharePath, isValidShareId, validateApiKey } from '../_lib/maintenance';

export async function POST(request: Request) {
  const secret = process.env.REVALIDATION_SECRET;

  const authError = validateApiKey(request, secret, 'Revalidation secret');
  if (authError) {
    return authError;
  }

  const { shareId } = (await request.json()) as { shareId?: string };
  if (!isValidShareId(shareId)) {
    return NextResponse.json({ error: 'shareId must be a 12-character hexadecimal ID' }, { status: 400 });
  }

  console.log(`Starting revalidation for shareId: ${shareId}`);
  revalidatePath(getSharePath(shareId));
  console.log(`Finished revalidation for shareId: ${shareId}`);

  return NextResponse.json({ revalidated: true, shareId });
}
