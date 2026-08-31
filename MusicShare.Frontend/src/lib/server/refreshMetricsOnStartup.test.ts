import { describe, expect, it, vi } from 'vitest';
import { refreshMetricsOnStartup, resolveApiOrigin } from './refreshMetricsOnStartup';

describe('refreshMetricsOnStartup', () => {
  it('prefers HTTPS and posts to the internal refresh endpoint without caching', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(null, { status: 202 }));
    const clearTimeout = vi.fn();
    const setTimeout = vi.fn().mockReturnValue(123);

    await refreshMetricsOnStartup({
      env: { services__api__https__0: 'https://api.internal', services__api__http__0: 'http://api.internal' },
      fetch,
      setTimeout,
      clearTimeout,
    });

    expect(fetch).toHaveBeenCalledWith('https://api.internal/api/metrics/refresh', {
      method: 'POST', cache: 'no-store', signal: expect.any(AbortSignal),
    });
    expect(setTimeout).toHaveBeenCalledWith(expect.any(Function), 120_000);
    expect(clearTimeout).toHaveBeenCalledWith(123);
  });

  it('uses the HTTP Aspire origin when HTTPS is unavailable', () => {
    expect(resolveApiOrigin({ services__api__http__0: 'http://api.internal' })).toBe('http://api.internal');
  });

  it.each([
    [{}, 'not configured', undefined],
    [{ services__api__https__0: 'not a url' }, 'invalid', expect.anything()],
  ])('contains missing or malformed configuration safely', async (env, expectedMessage, expectedError) => {
    const logger = { warn: vi.fn() };
    const fetch = vi.fn();

    await expect(refreshMetricsOnStartup({ env, fetch, logger })).resolves.toBeUndefined();

    expect(fetch).not.toHaveBeenCalled();
    expect(logger.warn).toHaveBeenCalledWith(expect.stringContaining(expectedMessage), expectedError);
  });

  it('contains HTTP and network failures', async () => {
    const logger = { warn: vi.fn() };
    const fetch = vi.fn()
      .mockResolvedValueOnce(new Response(null, { status: 503 }))
      .mockRejectedValueOnce(new Error('cold API'));
    const dependencies = { env: { services__api__https__0: 'https://api.internal' }, fetch, logger };

    await expect(refreshMetricsOnStartup(dependencies)).resolves.toBeUndefined();
    await expect(refreshMetricsOnStartup(dependencies)).resolves.toBeUndefined();

    expect(logger.warn).toHaveBeenCalledWith(expect.stringContaining('HTTP 503'), undefined);
    expect(logger.warn).toHaveBeenCalledWith(expect.stringContaining('request failed'), expect.any(Error));
  });

  it('aborts after the deadline and always clears the timer', async () => {
    const controller = new AbortController();
    const fetch = vi.fn().mockImplementation(async (_url: string, init: RequestInit) => {
      expect(init.signal).toBe(controller.signal);
      return new Response(null, { status: 202 });
    });
    const setTimeout = vi.fn((callback: () => void) => {
      callback();
      return 7;
    });
    const clearTimeout = vi.fn();

    await refreshMetricsOnStartup({
      env: { services__api__https__0: 'https://api.internal' }, fetch, createAbortController: () => controller, setTimeout, clearTimeout,
    });

    expect(controller.signal.aborted).toBe(true);
    expect(clearTimeout).toHaveBeenCalledWith(7);
  });
});
