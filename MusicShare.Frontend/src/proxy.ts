import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

const PROXY_TIMEOUT_MS = 120000; // 2 minutes to account for cold starts
const MAX_RETRIES = 3;
const INITIAL_RETRY_DELAY_MS = 2000;

async function fetchWithTimeout(
  url: string,
  options: RequestInit,
  timeoutMs: number
): Promise<Response> {
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

  try {
    const response = await fetch(url, {
      ...options,
      signal: controller.signal,
    });
    return response;
  } finally {
    clearTimeout(timeoutId);
  }
}

async function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function isRetryableError(error: unknown): boolean {
  if (error instanceof Error) {
    const message = error.message.toLowerCase();
    return (
      error.name === 'AbortError' ||
      message.includes('econnreset') ||
      message.includes('socket hang up') ||
      message.includes('econnrefused') ||
      message.includes('network') ||
      message.includes('fetch failed')
    );
  }
  return false;
}

async function proxyRequest(
  request: NextRequest,
  targetUrl: string
): Promise<Response> {
  const headers = new Headers();
  request.headers.forEach((value, key) => {
    // Skip headers that shouldn't be forwarded
    if (
      !['host', 'connection', 'keep-alive', 'transfer-encoding'].includes(
        key.toLowerCase()
      )
    ) {
      headers.set(key, value);
    }
  });

  const body = ['GET', 'HEAD'].includes(request.method)
    ? undefined
    : await request.arrayBuffer();

  let lastError: Error | null = null;

  for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
    try {
      const response = await fetchWithTimeout(
        targetUrl,
        {
          method: request.method,
          headers,
          body,
        },
        PROXY_TIMEOUT_MS
      );

      // Create a new response to forward back to the client
      const responseHeaders = new Headers();
      response.headers.forEach((value, key) => {
        // Skip hop-by-hop headers
        if (
          !['transfer-encoding', 'connection', 'keep-alive'].includes(
            key.toLowerCase()
          )
        ) {
          responseHeaders.set(key, value);
        }
      });

      return new NextResponse(response.body, {
        status: response.status,
        statusText: response.statusText,
        headers: responseHeaders,
      });
    } catch (error) {
      lastError = error instanceof Error ? error : new Error(String(error));

      const isLastAttempt = attempt === MAX_RETRIES;
      const shouldRetry = !isLastAttempt && isRetryableError(error);

      if (shouldRetry) {
        const delay = INITIAL_RETRY_DELAY_MS * Math.pow(2, attempt);
        console.log(
          `[Proxy] Attempt ${attempt + 1} failed, retrying in ${delay}ms:`,
          lastError.message
        );
        await sleep(delay);
      } else if (isLastAttempt) {
        console.error(
          `[Proxy] All ${MAX_RETRIES + 1} attempts failed:`,
          lastError.message
        );
      }
    }
  }

  // All retries exhausted, return an error response
  return NextResponse.json(
    {
      error: 'Service temporarily unavailable',
      message:
        'The API is starting up. Please try again in a moment.',
      details:
        process.env.NODE_ENV === 'development' ? lastError?.message : undefined,
    },
    { status: 503 }
  );
}

export async function proxy(request: NextRequest) {
  const apiTarget =
    process.env.services__api__https__0 ??
    process.env.services__api__http__0 ??
    'http://localhost:5222';

  const targetUrl = new URL(
    request.nextUrl.pathname + request.nextUrl.search,
    apiTarget
  ).toString();

  return proxyRequest(request, targetUrl);
}

export const config = {
  matcher: '/api/share/:path*',
};
