# Issue #84: Refresh public metrics when the frontend container starts

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/84
- Branch: `issue/84-frontend-startup-metrics-refresh`
- Status: Approved for implementation

## Request

When the public UI/Next.js Docker container starts, asynchronously contact the internal API so the API is awake and the existing full public-metrics recalculation runs. This must repair the observed production state where `/metrics` serves a cold, indefinitely cached zero snapshot.

## Repository findings

- Live `https://music.baileymiller.dev/metrics` currently returns HTTP 200 with `x-nextjs-cache: HIT`, `cache-control: s-maxage=31536000`, zero totals, and no weekly data. The deployed page is therefore serving its build-time empty fallback rather than a rebuilt snapshot.
- `MusicShare.Frontend/src/app/metrics/page.tsx` exports `revalidate = false`. Its production image is built before runtime Aspire service-discovery variables are available, so a failed build-time API fetch safely bakes the zero fallback into the standalone image until authenticated on-demand invalidation succeeds.
- The API already has the idempotent `RefreshPublicMetrics` MassTransit message, a single-concurrency/retried consumer that rebuilds the complete snapshot, and the authenticated frontend invalidation/retry path. The frontend should request that existing workflow rather than duplicate aggregation or cache logic.
- `MetricsController` currently exposes only the stored snapshot read. Adding `POST /api/metrics/refresh` to the private API can publish `RefreshPublicMetrics` and return HTTP 202 after the broker accepts the message; aggregation remains asynchronous in the consumer.
- The API remains private in Aspire/Azure Container Apps. The public Next proxy matcher is exactly `/api/share/:path*`, so `/api/metrics/refresh` must not be added to that matcher or otherwise exposed through the frontend.
- Aspire injects the internal API origin into the frontend as `services__api__https__0` or `services__api__http__0`; existing server-rendered pages already use this precedence.
- Next.js 16 supports `src/instrumentation.ts`; its `register` hook runs once for each new server instance. Because awaited `register` work delays readiness, the Node-only hook should start a self-contained best-effort promise and return immediately.
- The standalone Docker image already runs `node server.js`; no Docker entrypoint or shell-process changes are needed when the supported Next server lifecycle hook is used.

## Proposed implementation

Extend the private metrics controller with a `POST refresh` action that publishes exactly one `RefreshPublicMetrics` message with the request cancellation token and returns `Accepted()` only after MassTransit accepts the publish. Do not call `IPublicMetricsService.RefreshAsync` in the request and do not add a frontend proxy route.

Add a server-only frontend helper that resolves the injected internal API origin, builds `/api/metrics/refresh`, and performs a POST with no request body, `cache: no-store`, and a 120-second abort deadline consistent with the existing cold-start proxy allowance. It must catch and log missing configuration, malformed base URLs, non-success responses, aborts, and network failures without throwing or logging secrets. Export dependency seams for deterministic tests, but keep production usage simple.

Add `src/instrumentation.ts`. Its `register` function must act only when `NEXT_RUNTIME === 'nodejs'`, dynamically load the server helper, start it without awaiting the network operation, attach complete rejection handling, and return immediately so frontend readiness is independent of API cold-start latency. Each Node server instance invokes one startup request; duplicate instances remain safe because the existing message/snapshot flow is idempotent and versioned.

The successful sequence is: frontend server starts -> non-blocking internal POST -> API wakes/returns 202 after publishing -> existing consumer rebuilds the snapshot -> existing authenticated invalidation marks `/metrics` stale -> the next request regenerates the cached page from the stored snapshot.

## File-level plan

- `MusicShare.Api/Controllers/MetricsController.cs`: inject the MassTransit publish endpoint and add the internal asynchronous refresh action returning HTTP 202.
- `MusicShare.Tests/Unit/Api/Controllers/MetricsControllerTests.cs`: update controller construction and prove the POST publishes exactly one `RefreshPublicMetrics` message, forwards cancellation, returns 202, and does not run aggregation inline.
- `MusicShare.Frontend/src/instrumentation.ts`: add the Node-only, once-per-server startup hook with non-blocking dynamic helper invocation and rejection logging.
- `MusicShare.Frontend/src/instrumentation.test.ts`: prove Node registration starts one helper invocation without awaiting it and Edge/other runtimes do nothing; cover dynamic-import rejection handling if testable without production-only hooks.
- `MusicShare.Frontend/src/lib/server/refreshMetricsOnStartup.ts`: implement service-origin resolution, bounded no-store POST, safe logging, and complete failure containment.
- `MusicShare.Frontend/src/lib/server/refreshMetricsOnStartup.test.ts`: cover HTTPS/HTTP precedence, exact URL/method/cache/signal, missing or malformed configuration, HTTP failure, network failure, timeout cleanup/abort, and successful completion.

## Validation plan

- Run focused `MetricsControllerTests` and public-metrics consumer tests.
- Run `dotnet build MusicShare.slnx --configuration Release` and `dotnet test MusicShare.Tests/MusicShare.Tests.csproj --configuration Release`.
- Run focused frontend instrumentation/helper tests, then full `npm test`, `npm run lint`, and `npm run build` from `MusicShare.Frontend`.
- Inspect the standalone build output to confirm the instrumentation hook is included in the Node server bundle and no refresh code is shipped as browser behavior.
- If the local Docker daemon is available, build/run the frontend image against a capture endpoint and verify the container becomes ready independently while one POST reaches `/api/metrics/refresh`.
- Confirm `MusicShare.Frontend/src/proxy.ts` still matches only `/api/share/:path*` and the generated Aspire topology keeps the API private.
- Run `git diff --check` and confirm only issue-scoped changes are present.

## Risks and edge cases

- Awaiting the cold API request from `register` would extend or fail frontend readiness. The hook must deliberately detach the fully exception-contained helper promise.
- An unhandled detached rejection could terminate or destabilize Node depending on runtime settings. Both dynamic import and helper execution require explicit rejection handling, and the helper itself must not throw.
- The API service-discovery variable can be missing during `next build` or isolated frontend development. Skip safely rather than falling back to a public or localhost production target.
- A cold API may take materially longer than a normal request. Use a bounded 120-second deadline, but never block the frontend server on it.
- Multiple frontend replicas or restarts may publish duplicate refresh messages. The existing serialized, versioned snapshot path is authoritative; do not add cross-instance state or locks.
- Returning 202 means the refresh was queued, not completed. The existing consumer owns aggregation and frontend invalidation, so the startup helper must not poll for completion.
- The API endpoint is safe only while the API stays private and the frontend proxy matcher remains narrow. Deployment validation must preserve both boundaries.

## Definition of done

- [ ] Every frontend Node server startup initiates one non-blocking POST to the Aspire-internal `/api/metrics/refresh` endpoint.
- [ ] The endpoint publishes exactly one existing `RefreshPublicMetrics` message and returns HTTP 202 without aggregating inline.
- [ ] Startup request failure, timeout, invalid/missing configuration, or a cold API cannot delay or crash frontend readiness.
- [ ] Browser and Edge runtimes do not execute the startup refresh.
- [ ] The API refresh endpoint remains private and is not added to the public frontend proxy.
- [ ] Existing consumer, snapshot versioning, and `/metrics` invalidation behavior are reused unchanged.
- [ ] Focused backend/frontend tests cover success and failure paths, and all repository validation gates pass.
- [ ] The standalone build contains the Node instrumentation hook and no unexpected local changes remain.
