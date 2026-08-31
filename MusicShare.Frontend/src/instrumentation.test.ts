import { describe, expect, it, vi } from 'vitest';
import { startMetricsRefreshOnNodeStartup } from './instrumentation';

describe('startMetricsRefreshOnNodeStartup', () => {
  it('starts one Node refresh without awaiting it', async () => {
    let resolveRefresh: (() => void) | undefined;
    const refresh = vi.fn(() => new Promise<void>((resolve) => { resolveRefresh = resolve; }));
    const load = vi.fn().mockResolvedValue({ refreshMetricsOnStartup: refresh });

    startMetricsRefreshOnNodeStartup('nodejs', load);

    expect(load).toHaveBeenCalledOnce();
    await vi.waitFor(() => expect(refresh).toHaveBeenCalledOnce());
    resolveRefresh?.();
  });

  it.each(['edge', undefined])('does nothing outside the Node runtime', (runtime) => {
    const load = vi.fn();

    startMetricsRefreshOnNodeStartup(runtime, load);

    expect(load).not.toHaveBeenCalled();
  });

  it('contains dynamic import failures', async () => {
    const logger = { warn: vi.fn() };
    startMetricsRefreshOnNodeStartup('nodejs', () => Promise.reject(new Error('load failed')), logger);

    await vi.waitFor(() => expect(logger.warn).toHaveBeenCalledWith(
      expect.stringContaining('initialization failed'), expect.any(Error)
    ));
  });
});
