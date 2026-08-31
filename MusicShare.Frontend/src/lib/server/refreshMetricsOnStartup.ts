const refreshPath = '/api/metrics/refresh';
const timeoutMs = 120_000;

type Logger = Pick<Console, 'warn'>;

export type RefreshMetricsOnStartupDependencies = {
  env?: NodeJS.ProcessEnv;
  fetch?: typeof globalThis.fetch;
  logger?: Logger;
  createAbortController?: () => AbortController;
  setTimeout?: typeof globalThis.setTimeout;
  clearTimeout?: typeof globalThis.clearTimeout;
};

function warn(logger: Logger, message: string, error?: unknown) {
  try {
    logger.warn(message, error);
  } catch {
    // Startup refresh logging must never affect frontend readiness.
  }
}

export function resolveApiOrigin(env: NodeJS.ProcessEnv): string | undefined {
  return env.services__api__https__0 ?? env.services__api__http__0;
}

/**
 * Best-effort private API wake-up for a newly started Next.js Node server.
 * This never throws: the API's own bootstrap and the next frontend restart can retry.
 */
export async function refreshMetricsOnStartup(
  dependencies: RefreshMetricsOnStartupDependencies = {}
): Promise<void> {
  const env = dependencies.env ?? process.env;
  const fetchFn = dependencies.fetch ?? globalThis.fetch;
  const logger = dependencies.logger ?? console;
  const createAbortController = dependencies.createAbortController ?? (() => new AbortController());
  const setTimeoutFn = dependencies.setTimeout ?? globalThis.setTimeout;
  const clearTimeoutFn = dependencies.clearTimeout ?? globalThis.clearTimeout;
  const apiOrigin = resolveApiOrigin(env);

  if (!apiOrigin) {
    warn(logger, 'Metrics startup refresh skipped because the internal API origin is not configured.');
    return;
  }

  let url: string;
  try {
    url = new URL(refreshPath, apiOrigin).toString();
  } catch (error) {
    warn(logger, 'Metrics startup refresh skipped because the internal API origin is invalid.', error);
    return;
  }

  try {
    const controller = createAbortController();
    const timeout = setTimeoutFn(() => controller.abort(), timeoutMs);

    try {
      const response = await fetchFn(url, {
        method: 'POST',
        cache: 'no-store',
        signal: controller.signal,
      });

      if (!response.ok) {
        warn(logger, `Metrics startup refresh request was rejected with HTTP ${response.status}.`);
      }
    } catch (error) {
      warn(logger, 'Metrics startup refresh request failed.', error);
    } finally {
      clearTimeoutFn(timeout);
    }
  } catch (error) {
    warn(logger, 'Metrics startup refresh request could not be started.', error);
  }
}
