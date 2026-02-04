import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
  const apiTarget =
    process.env.services__api__https__0 ??
    process.env.services__api__http__0 ??
    'http://localhost:5222';

  const url = new URL(
    request.nextUrl.pathname + request.nextUrl.search,
    apiTarget
  );

  return NextResponse.rewrite(url);
}

export const config = {
  matcher: '/api/:path*',
};
