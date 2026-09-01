import { NextResponse } from 'next/server';
import { timingSafeEqual } from 'node:crypto';

const shareId = /^[a-f0-9]{12}$/;
const fingerprint = /^[a-f0-9]{64}$/;
const operationId = /^reconcile-[a-f0-9]{64}$/;
const maximumProviderIdLength = 256;
const maximumErrorLength = 512;

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
  if (value.changed && !value.success) return null;
  const countValue = value.affectedShareCount;
  if (!Number.isSafeInteger(countValue) || typeof countValue !== 'number') return null;
  const count = countValue;
  if (count < 0 || count > 2 || (value.success && count !== 2) || (!value.success && count !== 0)) return null;
  if (value.success) {
    if (typeof value.operationId !== 'string' || !operationId.test(value.operationId) ||
        typeof value.fingerprint !== 'string' || !fingerprint.test(value.fingerprint) ||
        typeof value.canonicalShareId !== 'string' || !shareId.test(value.canonicalShareId) ||
        typeof value.aliasShareId !== 'string' || !shareId.test(value.aliasShareId) ||
        value.canonicalShareId === value.aliasShareId || !Array.isArray(value.sharedIdentities) ||
        value.sharedIdentities.length < 1 || value.sharedIdentities.length > 6 ||
        !value.sharedIdentities.every(identity => validIdentity(identity))) return null;
    return {
      success: true, changed: value.changed, affectedShareCount: count, operationId: value.operationId,
      fingerprint: value.fingerprint, canonicalShareId: value.canonicalShareId, aliasShareId: value.aliasShareId,
      sharedIdentities: value.sharedIdentities.map(identity => {
        const item = identity as Record<string, unknown>;
        return { serviceType: item.serviceType, serviceSongId: item.serviceSongId };
      }),
    };
  }
  if (typeof value.error !== 'string' || !validText(value.error, maximumErrorLength)) return null;
  return { success: false, changed: false, affectedShareCount: 0, error: value.error };
}

function validIdentity(value: unknown): boolean {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
  const identity = value as Record<string, unknown>;
  return typeof identity.serviceType === 'number' && Number.isSafeInteger(identity.serviceType) && identity.serviceType >= 1 && identity.serviceType <= 3 &&
    typeof identity.serviceSongId === 'string' && validText(identity.serviceSongId, maximumProviderIdLength);
}

function validText(value: string, maximumLength: number): boolean {
  return value.length > 0 && value.length <= maximumLength && [...value].every(character => {
    const code = character.charCodeAt(0);
    return code > 31 && code !== 127;
  });
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
