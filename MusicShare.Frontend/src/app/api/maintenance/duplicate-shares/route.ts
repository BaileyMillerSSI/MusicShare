import { NextResponse } from 'next/server';

const shareId = /^[a-f0-9]{12}$/;
const fingerprint = /^[a-f0-9]{64}$/;

function validBody(body: unknown): body is { firstShareId: string; secondShareId: string; canonicalShareId?: string; mode: 'dry-run' | 'apply'; fingerprint?: string } {
  if (!body || typeof body !== 'object' || Array.isArray(body)) return false;
  const value = body as Record<string, unknown>;
  const allowed = new Set(['firstShareId', 'secondShareId', 'canonicalShareId', 'mode', 'fingerprint']);
  if (Object.keys(value).some(key => !allowed.has(key)) || !shareId.test(String(value.firstShareId)) || !shareId.test(String(value.secondShareId))) return false;
  if (value.firstShareId === value.secondShareId || (value.canonicalShareId !== undefined && (!shareId.test(String(value.canonicalShareId)) || (value.canonicalShareId !== value.firstShareId && value.canonicalShareId !== value.secondShareId)))) return false;
  if (value.mode !== 'dry-run' && value.mode !== 'apply') return false;
  return value.mode !== 'apply' || typeof value.fingerprint === 'string' && fingerprint.test(value.fingerprint);
}

export async function POST(request: Request) {
  const secret = process.env.MAINTENANCE_SECRET;
  if (!secret) return NextResponse.json({ error: 'Maintenance is not configured' }, { status: 503 });
  if (request.headers.get('X-MAINTENANCE-KEY') !== secret) return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
  let body: unknown;
  try { body = await request.json(); } catch { return NextResponse.json({ error: 'Invalid request' }, { status: 400 }); }
  if (!validBody(body)) return NextResponse.json({ error: 'Invalid request' }, { status: 400 });
  const apiBase = process.env.services__api__https__0 ?? process.env.services__api__http__0;
  if (!apiBase) return NextResponse.json({ error: 'Maintenance backend unavailable' }, { status: 503 });
  try {
    const response = await fetch(`${apiBase}/internal/maintenance/duplicate-shares/reconcile`, {
      method: 'POST', headers: { 'Content-Type': 'application/json', 'X-MAINTENANCE-KEY': secret }, body: JSON.stringify(body), signal: AbortSignal.timeout(10_000), cache: 'no-store'
    });
    const payload = await response.json().catch(() => null);
    if (!response.ok || !payload || typeof payload !== 'object') return NextResponse.json({ error: 'Reconciliation failed' }, { status: response.status >= 500 ? 502 : 400 });
    return NextResponse.json(payload);
  } catch { return NextResponse.json({ error: 'Maintenance backend unavailable' }, { status: 502 }); }
}
