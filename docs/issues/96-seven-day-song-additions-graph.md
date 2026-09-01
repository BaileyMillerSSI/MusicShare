# Issue #96: Show song additions for the last seven days

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/96
- Branch: `issue/96-seven-day-song-additions-graph`
- Status: Approved for implementation

## Request

Replace the current eight-week presentation on `/metrics` with one graph showing song additions for the latest seven days. Keep the existing newest-first list of the last 20 songs directly below the graph.

## Repository findings

- `MusicShare.Frontend/src/app/metrics/page.tsx` currently renders three summary cards, an eight-bucket `WeeklyCompletedSongsChart`, and then `recentSongs.slice(0, 20)`. The recent-song list already has the requested size, order, canonical `/share/{shareId}` links, artwork, title, and artists.
- The current chart is backed by `weeklyCompletedSongs`: eight chronological Sunday-UTC buckets persisted inside the singleton public metrics snapshot. The last bucket drives `+N this week` on the page and in social-preview copy.
- `PublicMetricsService.RefreshAsync` captures one UTC timestamp, aggregates distinct eligible canonical songs, zero-fills the bounded series, and persists it through the existing revision-protected snapshot write. This event-driven path is the correct place to add daily data; public reads must remain aggregation-free.
- `ShareRequestRepository.GetCompletedDistinctSongCountsByWeekAsync` starts from the canonical completed-song pipeline, deduplicates songs, validates public sources and real song rows, uses canonical `Song.CreatedAt`, and then applies MongoDB `$dateTrunc`. Its MongoDB compatibility fallback applies the same canonical pipeline before in-memory grouping. Daily aggregation must preserve those semantics.
- `PublicMetricsWeeklyRefreshService` publishes a refresh at Sunday 00:00 UTC even when no song completes. A seven-calendar-day window instead needs a refresh at every UTC midnight so an expired day rolls off truthfully during idle periods. The private API already remains at one minimum production replica for this boundary task.
- Existing Mongo snapshots contain `WeeklyCompletedSongs`. Because removing a mapped C# property can make old BSON fields unreadable, the persistence entity should retain that legacy property for deserialization during migration, but new snapshots/API/UI should use `DailyCompletedSongs` and stop populating/rendering weekly data.
- `publicMetrics.ts` validates the optional series and derives summary/share-preview values. The new seven-day total should be the sum of the seven daily buckets, replacing `thisWeekCompletedSongs` in the page card, metadata, preview version, and generated share image so all public copy matches the graph.
- The current chart became a client component only to localize UTC week boundaries after hydration. Daily buckets are explicitly UTC calendar days; deterministic UTC date labels avoid shifting a bucket onto a different local date and let the graph remain a server-safe presentational component.
- Frontend validation uses Vitest/Testing Library, ESLint, and the Next.js production build. Backend coverage uses xUnit plus real Mongo integration tests. The repository has no Playwright suite, so responsive graph/recent-list validation should use a running page and browser inspection.

## Proposed implementation

### Persist seven UTC daily buckets

Replace the service/API weekly series with `DailyCompletedSongs`, containing exactly seven oldest-to-newest buckets: the previous six complete UTC calendar days plus the current partial UTC day. Each bucket contains `DayStart` at UTC midnight and a non-negative distinct canonical-song count. Capture one `generatedAt` value per refresh, calculate `currentDayStart = generatedAt.Date`, request `[currentDayStart - 6 days, currentDayStart + 1 day)`, and zero-fill every missing date.

Rename the repository query and record to daily terminology. Use `$dateTrunc` with `unit: "day"` and `timezone: "UTC"`; keep the same canonical completed-song pipeline, range guards, deterministic ordering, defensive materialization, and compatibility fallback. The fallback groups canonical dates by their UTC `.Date`.

Add `DailyCompletedSongs` to `PublicMetricsSnapshot` and retain `WeeklyCompletedSongs` only as a legacy persistence field so existing BSON documents deserialize safely. New candidates leave the legacy field empty. Replace the weekly DTO in `PublicMetricsResponse` with the daily DTO. Empty/legacy snapshots map to an empty daily list until startup/event refresh writes the new series.

Rename `PublicMetricsWeeklyRefreshService` to `PublicMetricsDailyRefreshService`. Schedule the next strictly future UTC midnight (`utcNow.Date.AddDays(1)`), then publish through the same retry/cancellation loop. Register the renamed service and update production topology documentation from Sunday/weekly to daily/UTC-midnight; replica counts do not change.

### Render one seven-day graph

Replace `WeeklyCompletedSongsChart` with `DailySongAdditionsChart`. Render a single shared plot area containing seven daily bars, not seven separate background panels. Each bar exposes its exact count visibly, uses a deterministic UTC date label, preserves a true zero height for zeroes, and marks the last bucket `Today`. Scale positive heights relative to the largest count with an all-zero guard, while keeping all seven columns within the shared graph frame at phone widths. Add concise copy that days use UTC.

Use the heading `Songs added in the last 7 days`. If daily data is absent during migration or the API is unavailable, keep a factual unavailable empty state instead of fabricating seven zero buckets in the browser. Do not add polling, controls, a chart dependency, or browser-side fetching.

Derive `lastSevenDaysCompletedSongs` by summing the daily buckets. Change the Songs summary subline to `+N in the last 7 days`, and update available-snapshot metadata, preview versioning, alt text, and generated share-image label from `Completed this week` to `Added in the last 7 days`. This keeps every public summary consistent with the new graph.

Leave the existing `Recently added` implementation directly after the graph. It must continue slicing to 20 and preserving the backend's canonical newest-first order.

## File-level plan

- `MusicShare.Persistence/Entities/PublicMetricsSnapshot.cs`: add the daily bucket entity/list; retain the weekly list only as a legacy BSON compatibility field that new candidates do not populate.
- `MusicShare.Persistence/Repositories/IShareRequestRepository.cs`: replace the weekly query/record contract with daily terminology.
- `MusicShare.Persistence/Repositories/ShareRequestRepository.cs`: change the bounded canonical aggregation and compatibility fallback to UTC-day grouping/materialization.
- `MusicShare.Services/Models/PublicMetricsResponse.cs`: replace the weekly response series/DTO with the daily series/DTO and preserve a safe empty response.
- `MusicShare.Services/Services/PublicMetricsService.cs`: request and zero-fill seven UTC calendar-day buckets, persist/map daily data, stop generating weekly data, and expose a UTC-day boundary helper.
- `MusicShare.Api/Services/PublicMetricsWeeklyRefreshService.cs`: rename to `PublicMetricsDailyRefreshService.cs` and schedule/retry refresh publication at every next UTC midnight.
- `MusicShare.Api/Program.cs`: register the renamed daily refresh hosted service.
- `MusicShare.Frontend/src/lib/api.ts`: replace optional weekly response types with optional daily bucket types.
- `MusicShare.Frontend/src/lib/server/publicMetrics.ts`: validate daily buckets, provide the empty daily series, derive the seven-day sum, and update preview copy/version data.
- `MusicShare.Frontend/src/app/metrics/WeeklyCompletedSongsChart.tsx`: replace with `DailySongAdditionsChart.tsx`, a deterministic accessible one-graph/seven-bar UTC presentation.
- `MusicShare.Frontend/src/app/metrics/page.tsx`: render the daily graph and seven-day summary copy before the unchanged maximum-20 recent list.
- `MusicShare.Frontend/src/app/metrics/share-image/MetricsShareImage.tsx`: label the fourth statistic as the last-seven-days addition total.
- `MusicShare.Tests/Unit/Persistence/ShareRequestRepositoryMetricsTests.cs`: update pipeline/materialization/argument tests for UTC days and malformed rows.
- `MusicShare.Tests/Integration/PublicMetricsMongoRepositoryTests.cs`: prove canonical `Song.CreatedAt` day assignment across UTC midnight, distinct/excluded/malformed behavior, daily order, and repository test-double compatibility.
- `MusicShare.Tests/Unit/Services/PublicMetricsServiceTests.cs`: cover seven-bucket zero fill/order, current UTC day, persistence/mapping, empty/legacy snapshots, and UTC-only boundary calculation.
- `MusicShare.Tests/Unit/Api/Services/PublicMetricsWeeklyRefreshServiceTests.cs`: rename for the daily service and cover next-midnight scheduling, exact-midnight behavior, publish retry, cancellation, and UTC enforcement.
- `MusicShare.Frontend/src/app/metrics/WeeklyCompletedSongsChart.test.tsx`: replace with daily-graph tests for one shared graph, seven dates/counts, zero and all-zero heights, `Today`, UTC labels, scaling, and accessible names.
- `MusicShare.Frontend/src/app/metrics/page.test.tsx`: replace weekly fixtures/assertions with seven daily buckets; verify heading, seven-day summary, graph placement before recent songs, safe missing-data state, and the unchanged 20-song bound/order.
- `MusicShare.Frontend/src/lib/server/publicMetrics.test.ts`, `MusicShare.Frontend/src/app/metrics/share-image/MetricsShareImage.test.tsx`, and `MusicShare.Frontend/src/app/metrics/share-image/route.test.ts`: update validation, seven-day derivation/version/copy, image label/value, and malformed daily payload coverage.
- `README.md`, `MusicShare.AppHost/AppHost.cs`, `docs/02-architecture.md`, `docs/04-aspire.md`, and `docs/08-presentation-script.md`: describe the private API's daily UTC-midnight refresh while preserving the existing minimum-replica/cost disclosure.

## Validation plan

- `dotnet test MusicShare.Tests/MusicShare.Tests.csproj --filter "FullyQualifiedName~PublicMetrics"`
- `dotnet build MusicShare.slnx --configuration Release`
- `dotnet test MusicShare.Tests/MusicShare.Tests.csproj --configuration Release`
- `cd MusicShare.Frontend && npm test -- src/app/metrics/DailySongAdditionsChart.test.tsx src/app/metrics/page.test.tsx src/lib/server/publicMetrics.test.ts src/app/metrics/share-image/MetricsShareImage.test.tsx src/app/metrics/share-image/route.test.ts`
- `cd MusicShare.Frontend && npm test`
- `cd MusicShare.Frontend && npm run lint`
- `cd MusicShare.Frontend && npm run build`
- `git diff --check`
- Run representative seven-day data through `/metrics` and inspect desktop and phone widths. Confirm there is one shared graph with seven readable UTC-day buckets, zeroes stay at zero height, `Today` is clear, the 20-song list remains immediately below it, and `document.documentElement.scrollWidth <= window.innerWidth`.

## Risks and edge cases

- “Last seven days” is defined as seven UTC calendar buckets including the current partial day, not a rolling 168-hour window. Labels and explanatory copy must make the UTC basis visible.
- At exactly UTC midnight, the just-ended day must shift left and the new current day must appear as zero even if no song completes; the boundary service must choose a strictly future midnight to avoid a tight loop.
- Canonical song creation dates can differ from share-request dates. Daily graph counts and recent ordering must continue to use canonical `Song.CreatedAt` and must not reintroduce request-time chronology.
- Duplicate completed shares count once. Unknown-source, invalid/missing song, malformed-date, pending, and failed rows remain excluded.
- Existing persisted documents contain weekly fields but no daily field. They must deserialize without errors and return an empty daily series until an accepted refresh replaces the snapshot.
- One unusually large day must not make positive small-day bars disappear; zero remains a real zero, and all-zero data must not divide by zero.
- The recent list is already capped to 20 in both backend and frontend. Refactoring the graph must not reorder, relocate below unrelated content, or expand that list.

## Definition of done

- [ ] The persisted/internal metrics contract carries exactly seven oldest-to-newest UTC daily buckets including today; weekly data is no longer generated or rendered.
- [ ] Daily aggregation preserves canonical date, distinct-song, public-source, valid-song, range, zero-fill, and deterministic-order semantics.
- [ ] An idle deployment publishes a refresh at every UTC midnight with bounded retry/cancellation behavior and no public aggregation or polling.
- [ ] `/metrics` renders one shared seven-day graph with visible dates and counts, zero handling, accessible labels, and a clearly identified current day.
- [ ] Page, metadata, and share-preview summaries consistently report additions in the last seven days rather than the current week.
- [ ] The existing newest-first canonical `Recently added` list remains directly below the graph and renders no more than 20 songs.
- [ ] Legacy/missing daily snapshots and malformed/unavailable payloads remain safe during migration.
- [ ] Focused/full backend and frontend tests, Release build, lint, Next.js build, and `git diff --check` pass.
- [ ] Desktop and phone inspection confirms the intended graph/list hierarchy and no horizontal overflow.
