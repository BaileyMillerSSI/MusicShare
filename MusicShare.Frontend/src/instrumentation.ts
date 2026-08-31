type StartupRefresh = () => Promise<void>;
type StartupRefreshLoader = () => Promise<{ refreshMetricsOnStartup: StartupRefresh }>;
type Logger = Pick<Console, 'warn'>;

function warn(logger: Logger, error: unknown) {
  try {
    logger.warn('Metrics startup refresh initialization failed.', error);
  } catch {
    // Logging a best-effort startup request must not affect frontend readiness.
  }
}

export function startMetricsRefreshOnNodeStartup(
  runtime: string | undefined,
  loadStartupRefresh: StartupRefreshLoader,
  logger: Logger = console
) {
  if (runtime !== 'nodejs') return;

  void loadStartupRefresh()
    .then(({ refreshMetricsOnStartup }) => refreshMetricsOnStartup())
    .catch((error: unknown) => warn(logger, error));
}

export async function register() {
  startMetricsRefreshOnNodeStartup(
    process.env.NEXT_RUNTIME,
    () => import('./lib/server/refreshMetricsOnStartup')
  );
}
