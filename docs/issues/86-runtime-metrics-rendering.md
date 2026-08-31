# Issue #86: Render public metrics from the runtime snapshot after cold start

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/86
- Branch: `issue/86-runtime-metrics-rendering`
- Status: Approved for implementation

## Request

Ensure a freshly started frontend container cannot keep serving the zero-valued metrics page embedded during the Docker build. Retain the asynchronous startup request added by issue #84, but make the public metrics route read the persisted API snapshot at request time.

## Repository and production findings

- The production API snapshot rebuilt successfully after the frontend startup request and contained 289 completed songs, including 6 in the current Sunday-UTC week.
- The first public request still returned the build-time zero page with `x-nextjs-cache: HIT`; a second metrics refresh changed the response to `MISS` and rendered the correct totals and chart.
- `MusicShare.Frontend/src/app/metrics/page.tsx` exports `revalidate = false`, so Next pre-renders it while the private API is unavailable during image construction.
- The existing API snapshot endpoint is already a cheap read from the singleton `publicMetricsSnapshots` document; aggregation remains asynchronous and is not part of page rendering.
- The frontend has an Aspire-internal API origin at runtime and must not expose the API through its public proxy.

## Proposed implementation

Mark the metrics page as request-time rendered with the supported Next route-segment configuration. Keep its existing server-only API fetch, response validation, zero fallback, UI, and weekly chart unchanged. This prevents the image build from becoming the authoritative source of public metrics while preserving graceful behavior when the private API is genuinely unavailable.

Retain the startup instrumentation and private refresh endpoint from issue #84. The startup request warms the scale-to-zero API and queues a fresh snapshot; the request-time metrics render then reads that stored snapshot directly instead of depending on invalidating a build artifact.

## File-level plan

- `MusicShare.Frontend/src/app/metrics/page.tsx`: replace the permanent static route configuration with request-time rendering.
- `MusicShare.Frontend/src/app/metrics/page.test.tsx`: assert the route remains request-time rendered and preserve existing UI/fallback/chart coverage.

## Validation plan

- Run the focused metrics page and startup instrumentation tests.
- Run the complete frontend test suite, lint, and a clean locked production build.
- Inspect build output to confirm `/metrics` is dynamic while the rest of the intended routes retain their existing behavior.
- Confirm startup instrumentation still exists in the standalone server bundle and the public proxy remains limited to share routes.
- Run `git diff --check` and inspect the issue-scoped diff.
- After merge and deployment, verify the production response reports the API snapshot totals and weekly chart without requiring a second refresh.

## Risks and edge cases

- Request-time rendering makes each metrics page request read the singleton snapshot. This is intentionally bounded to the metrics route and avoids expensive aggregation.
- The API can be scaled to zero. The startup instrumentation begins waking it as the frontend starts; the page already catches API failures and safely returns its zero fallback.
- The runtime route must remain server-rendered. Do not move the fetch into browser code or widen the frontend API proxy.
- Existing authenticated revalidation remains harmless and continues to support other snapshot-triggered flows, even though this page no longer depends on a permanent full-route cache entry.

## Definition of done

- [ ] `/metrics` is not pre-rendered with build-time API fallback data.
- [ ] Each metrics page render reads the persisted internal API snapshot on the server.
- [ ] Existing totals, weekly delta/chart, recent songs, response validation, and zero fallback behavior are preserved.
- [ ] Startup API warm-up and the private Aspire boundary remain unchanged.
- [ ] Focused and full frontend validation pass from the locked dependency graph.
- [ ] Production shows the nonzero snapshot after a fresh deployment without a second manual refresh.
