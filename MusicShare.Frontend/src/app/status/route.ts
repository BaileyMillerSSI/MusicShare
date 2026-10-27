import { createHash, timingSafeEqual } from 'node:crypto';
import build from '../../lib/server/status-build.json';
import { statusBuild } from '../../lib/server/status-metadata';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

const headers = {
  'Cache-Control': 'private, no-store',
  'CDN-Cache-Control': 'no-store',
  'Cloudflare-CDN-Cache-Control': 'no-store',
};
const digest = (value: string) => createHash('sha256').update(value).digest();

function authentication(request: Request): number {
  const current = process.env.STATUS_TOKEN_CURRENT;
  const previous = process.env.STATUS_TOKEN_PREVIOUS;
  const validToken = (token: string | undefined): token is string => typeof token === 'string' && /^[A-Za-z0-9_-]{32,256}$/.test(token);
  if (!validToken(current) || (previous && !validToken(previous))) return 503;
  const authorization = request.headers.get('authorization') ?? '';
  if (authorization.length > 512 || !/^Bearer [A-Za-z0-9_-]{32,256}$/.test(authorization)) return 401;
  const supplied = digest(authorization.slice(7));
  // Evaluate both comparisons without short-circuiting during rotation.
  const matchesCurrent = timingSafeEqual(supplied, digest(current));
  const matchesPrevious = timingSafeEqual(supplied, digest(previous || current));
  return matchesCurrent || matchesPrevious ? 200 : 401;
}

function respond(request: Request, head: boolean): Response {
  const status = authentication(request);
  if (status !== 200) return new Response(head ? null : '{"error":"Unavailable"}', {
    status, headers: { ...headers, 'Content-Type': 'application/json' },
  });
  if (head) return new Response(null, { headers });
  const environment = process.env.STATUS_ENVIRONMENT ?? 'production';
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*(?![\s\S])/.test(environment) || environment.length > 32) {
    return new Response('{"error":"Unavailable"}', { status: 503, headers: { ...headers, 'Content-Type': 'application/json' } });
  }
  return Response.json({
    schemaVersion: 2,
    service: 'musicshare',
    environment,
    build: statusBuild(build),
    deployment: { id: null, deployedAt: null },
    health: {
      state: 'healthy',
      coverage: ['web-process'],
      components: [{ name: 'web-process', state: 'healthy' }],
    },
    sampledAt: new Date().toISOString(),
  }, { headers });
}

export function GET(request: Request) { return respond(request, false); }
export function HEAD(request: Request) { return respond(request, true); }
