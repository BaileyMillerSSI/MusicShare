import { NextResponse } from 'next/server';
import { timingSafeEqual } from 'node:crypto';

const shareId = /^[a-f0-9]{12}$/;
const fingerprint = /^[a-f0-9]{64}$/;

function validBody(body: unknown): body is { firstShareId: string; secondShareId: string; canonicalShareId?: string; mode: 'dry-run' | 'apply'; fingerprint?: string } {
  if (!body || typeof body !== 'object' || Array.isArray(body)) return false;
  const value = body as Record<string, unknown>;
  const allowed = new Set(['firstShareId', 'secondShareId', 'canonicalShareId', 'mode', 'fingerprint']);
  if (Object.keys(value).some(key => !allowed.has(key)) || typeof value.firstShareId !== 'string' || typeof value.secondShareId !== 'string' || !shareId.test(value.firstShareId) || !shareId.test(value.secondShareId)) return false;
  if (value.canonicalShareId !== undefined && (typeof value.canonicalShareId !== 'string' || !shareId.test(value.canonicalShareId) || (value.canonicalShareId !== value.firstShareId && value.canonicalShareId !== value.secondShareId))) return false;
  if (value.firstShareId === value.secondShareId) return false;
  if (value.mode !== 'dry-run' && value.mode !== 'apply') return false;
  return value.mode !== 'apply' || typeof value.fingerprint === 'string' && fingerprint.test(value.fingerprint);
}

function secretsMatch(supplied: string | null, expected: string): boolean {
  if (supplied === null) return false;
  const suppliedBytes = Buffer.from(supplied);
  const expectedBytes = Buffer.from(expected);
  return suppliedBytes.length === expectedBytes.length && timingSafeEqual(suppliedBytes, expectedBytes);
}

function project(payload: unknown): Record<string, unknown> | null {
  if (!payload || typeof payload !== 'object' || Array.isArray(payload)) return null;
  const value = payload as Record<string, unknown>;
  if (typeof value.success !== 'boolean' || typeof value.changed !== 'boolean') return null;
  const result: Record<string, unknown> = { success: value.success, changed: value.changed };
  for (const key of ['error', 'operationId', 'fingerprint', 'canonicalShareId', 'aliasShareId']) if (typeof value[key] === 'string') result[key] = value[key];
  if (Array.isArray(value.sharedIdentities) && value.sharedIdentities.length <= 8 && value.sharedIdentities.every(x => x && typeof x === 'object' && typeof (x as Record<string, unknown>).serviceType === 'number' && typeof (x as Record<string, unknown>).serviceSongId === 'string')) result.sharedIdentities = value.sharedIdentities.map(x => ({ serviceType: (x as Record<string, unknown>).serviceType, serviceSongId: (x as Record<string, unknown>).serviceSongId }));
  return result;
}

export async function POST(request: Request) {
  const secret = process.env.MAINTENANCE_SECRET;
  if (!secret) return NextResponse.json({ error: 'Maintenance is not configured' }, { status: 503 });
  if (!secretsMatch(request.headers.get('X-MAINTENANCE-KEY'), secret)) return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
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
    const result = project(payload);
    if (!response.ok || !result) return NextResponse.json({ error: 'Reconciliation failed' }, { status: response.status >= 500 ? 502 : 400 });
    return NextResponse.json(result);
  } catch { return NextResponse.json({ error: 'Maintenance backend unavailable' }, { status: 502 }); }
}
