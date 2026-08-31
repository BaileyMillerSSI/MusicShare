# Issue #82: Add weekly completed-song change chart to metrics

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/82
- Branch: `issue/82-weekly-completed-song-metrics`
- Status: Approved for implementation

## Request

Expand the public metrics page with a small, uncomplicated chart of completed-song change per week. Beneath the Completed songs total, show a value such as `+5 this week`, meaning five distinct new completed songs since the most recent Sunday at 00:00 UTC.

## Repository findings

- `MusicShare.Frontend/src/app/metrics/page.tsx` is a server-rendered, indefinitely cached page. It reads the internal API snapshot and is invalidated only by the existing authenticated event-driven refresh flow; this change must preserve that boundary and must not add client polling.
- `PublicMetricsService` rebuilds a singleton `PublicMetricsSnapshot` whenever a share saga completes. The stored snapshot currently contains total completed songs, per-service resolved-link counts, and a bounded recent-song list.
- `ShareRequestRepository.DistinctCompletedPipeline()` is the authoritative basis for the total and recent metrics. It filters to completed rows with valid object-id songs and known public source services, then groups by song so duplicate shares cannot inflate totals.
- A weekly "new songs" series must assign each distinct song to only one week. Use the earliest `createdAt` among its completed, valid, public-source share requests; a later duplicate share must not move or recount the song. `createdAt` is also the timestamp already used by the metrics page's "Recently added" data.
- MongoDB 8 is used in CI, so `$dateTrunc` with `startOfWeek: Sunday` and UTC is available. The existing `(status, createdAt, songId)` metrics index supports the bounded aggregation without a new index or bootstrap change.
- The frontend has no charting library. Tailwind and semantic HTML are sufficient for a compact bar chart and avoid adding dependency or client-component weight.
- The existing public API response is consumed defensively. Weekly data must be optional on the TypeScript side and default safely when an older or not-yet-bootstrapped snapshot has no weekly field.
- The first implementation added an in-process Sunday refresh, but production currently permits the private API to scale to zero. Because the API owns this exact-time scheduler, the published Aspire topology must guarantee at least one API replica; the public frontend may continue to scale to zero.

## Proposed implementation

Add a repository aggregation that groups all eligible completed requests by song, selects the earliest completion-eligible `createdAt`, filters those distinct-song timestamps to an explicit eight-week range, and groups them into Sunday-starting UTC weeks. Materialize only valid UTC week/count rows.

During each public metrics refresh, capture one UTC generation time, calculate the current Sunday 00:00 UTC boundary, request the range covering the current partial week plus the previous seven weeks, and zero-fill every bucket in chronological order. Store those eight buckets in the existing singleton snapshot and expose them through the internal API response. Legacy snapshots with no list map to an empty series without failing.

On `/metrics`, show the final/current bucket as `+N this week` beneath Completed songs. Add a semantic "Completed songs by week" section containing eight lightweight CSS bars with visible counts and UTC week-start labels. Normalize bar heights against the largest displayed value, handle an all-zero series without division errors, and retain readable labels/values for assistive technology and narrow screens. Do not add chart interactivity or a client component.

Publish the private API with a minimum of one replica and clamp its maximum to at least that minimum. This is the smallest reliable deployment contract for the existing in-process weekly scheduler and keeps API/RabbitMQ communication internal. Document that `AZURE_API_MIN_REPLICAS=0` is no longer supported and that the private API has a small always-on production cost; do not change frontend scaling.

## File-level plan

- `MusicShare.Persistence/Entities/PublicMetricsSnapshot.cs`: add the stored bounded weekly completed-song bucket collection and entity type with safe empty defaults.
- `MusicShare.Persistence/Repositories/IShareRequestRepository.cs`: add the explicit UTC range aggregation contract and weekly count record.
- `MusicShare.Persistence/Repositories/ShareRequestRepository.cs`: implement the distinct-first-song weekly MongoDB aggregation, Sunday UTC bucketing, deterministic ordering, argument guards, and defensive row materialization.
- `MusicShare.Services/Models/PublicMetricsResponse.cs`: add weekly bucket DTOs to the internal response while preserving a safe pre-bootstrap empty response.
- `MusicShare.Services/Services/PublicMetricsService.cs`: calculate the Sunday UTC boundary, request eight weeks, zero-fill missing weeks, persist the bounded series, and map legacy/current snapshots safely.
- `MusicShare.Frontend/src/lib/api.ts`: add the optional weekly response field and bucket type for backward-compatible frontend consumption.
- `MusicShare.Frontend/src/app/metrics/page.tsx`: validate optional weekly data, render `+N this week`, and add the compact accessible responsive bar chart without a dependency or client polling.
- `MusicShare.Api/Services/PublicMetricsWeeklyRefreshService.cs`: retain exact Sunday UTC scheduling and retry the reached rollover publication until success or cancellation.
- `MusicShare.Api/Program.cs`: register the weekly refresh hosted service.
- `MusicShare.AppHost/AppHost.cs`: enforce a published private API minimum of one replica and a maximum no lower than the enforced minimum while preserving frontend scale-to-zero.
- `README.md`, `docs/02-architecture.md`, `docs/04-aspire.md`, and `docs/08-presentation-script.md`: update scaling documentation so it no longer claims the API can scale to zero and calls out the always-on scheduler/cost tradeoff.
- `MusicShare.Tests/Unit/Persistence/ShareRequestRepositoryMetricsTests.cs`: cover pipeline UTC/Sunday semantics, earliest distinct-song selection, materialization, and malformed rows/arguments.
- `MusicShare.Tests/Integration/PublicMetricsMongoRepositoryTests.cs`: prove real MongoDB weekly aggregation across Sunday boundaries, empty weeks, duplicates, and excluded rows; update repository test doubles for the new interface member.
- `MusicShare.Tests/Unit/Services/PublicMetricsServiceTests.cs`: cover deterministic Sunday boundary calculation, eight-bucket zero filling/order, persistence/mapping, and legacy/empty snapshots.
- `MusicShare.Frontend/src/app/metrics/page.test.tsx`: cover the current-week delta, weekly bars/labels including zeroes, optional legacy payloads, and malformed weekly payload rejection.
- `MusicShare.Tests/Unit/Api/Services/PublicMetricsWeeklyRefreshServiceTests.cs`: cover boundary scheduling, failure retry, and cancellation without requiring a song completion.

## Validation plan

- Run `dotnet test MusicShare.Tests/MusicShare.Tests.csproj` with the focused public-metrics unit/integration coverage.
- Run `dotnet build MusicShare.slnx --configuration Release`.
- Run `npm test -- --run src/app/metrics/page.test.tsx` from `MusicShare.Frontend` during iteration, then the full `npm test` suite.
- Run `npm run lint` and `npm run build` from `MusicShare.Frontend`.
- Inspect the rendered metrics layout at narrow mobile and desktop widths, confirming eight readable bars, `+N this week`, and no horizontal overflow.
- Generate or inspect the Aspire deployment manifest/Bicep output to confirm the API minimum is at least one even when the configured value is zero, the maximum is valid, the frontend scaling remains unchanged, and only the frontend is public.
- Run `git diff --check` and confirm the final branch contains only issue-scoped changes.

## Risks and edge cases

- Sunday boundaries must be computed from UTC, not server or viewer local time. Capture a single refresh timestamp so boundary calculations and `GeneratedAt` cannot disagree during a rollover.
- Duplicate completed requests for one song must count once in the week of that song's earliest eligible completed request; filtering the range before de-duplication would incorrectly count later re-shares as new.
- Existing snapshots do not contain weekly data. Backend mapping and frontend validation must tolerate the missing field until the first post-deploy refresh.
- Weeks with no additions must remain explicit zero buckets so the chart's spacing and current-week value are stable. A zero count must render at zero bar height even when another week has activity; an all-zero chart must not divide by zero.
- `createdAt` records request creation rather than a separate completion timestamp. This matches the existing "Recently added" metric and avoids a schema/backfill expansion; a request spanning the UTC boundary remains assigned to its creation week.
- Snapshot refreshes can overlap. The existing reserved version/non-regression write remains authoritative; weekly data is part of the same accepted candidate and must not introduce a second write path.
- Exact idle-week rollover now requires one continuously available private API replica. This slightly increases baseline production cost, but avoids an external scheduler, a public backend endpoint, or client polling. Multiple API replicas may publish duplicate refresh messages; the existing versioned/idempotent snapshot path remains authoritative.

## Definition of done

- [ ] The Completed songs card shows `+N this week` from the current Sunday 00:00 UTC bucket.
- [ ] `/metrics` shows a simple accessible eight-week bar chart, oldest to newest, including zero-count weeks and the current partial week.
- [ ] Zero-count weeks render with zero-height bars; the minimum visible height applies only to positive counts.
- [ ] Each valid distinct completed song contributes to exactly one weekly bucket based on its earliest eligible `createdAt`; duplicates and excluded rows do not inflate the series.
- [ ] The event-driven singleton snapshot/API carries the bounded weekly series without adding polling, a public aggregation route, or a chart dependency.
- [ ] The published Aspire topology guarantees at least one private API replica for exact Sunday rollover, keeps the frontend as the only public service, and documents the scale/cost change.
- [ ] Missing legacy weekly data and an unavailable/malformed metrics response render a safe zero/empty state.
- [ ] Focused backend repository/service and frontend page tests cover UTC rollover, zero filling, de-duplication, current-week display, and payload validation.
- [ ] Backend build/tests and frontend test/lint/build gates pass, and the responsive chart has no horizontal overflow.
