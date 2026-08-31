# Issue #78: Add event-driven public music metrics page

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/78
- Branch: `issue/78-event-driven-public-music-metrics`
- Status: Approved for implementation

## Request

Add a public metrics page that shows counts of completed songs by their original source service and a bounded newest-first list of recently added songs. Recent items must point to MusicShare's canonical `/share/{shareId}` pages. The page should use runtime static regeneration, but neither browser refreshes nor unauthenticated callers may trigger database aggregation or arbitrary cache invalidation. Refresh work must be event-driven after the song and its service links have reached a stable workflow state.

## Repository findings

- The Next.js App Router frontend is the only public service. Browser `/api/share/*` traffic is proxied to the Aspire-internal API by `MusicShare.Frontend/src/proxy.ts`; new backend metrics reads must remain outside that matcher.
- Share pages already use indefinite ISR (`revalidate = false`) and the secret-protected `POST /api/revalidate` route. `CompleteSagaActivity` calls `IFrontendRevalidateService` after the song and share request statuses are finalized.
- `SourceMetadataConsumer` inserts the `Song` and source `SongServiceLink`; service-specific consumers insert resolved links; `ShareRequestSaga` calls `CompleteSagaActivity` once all configured target services have resolved or failed. Saga completion is therefore the first single event boundary at which the song and all attempted links are stable.
- `ShareRequest` contains the canonical `ShareId`, `SongId`, `SourceService`, `Status`, and `CreatedAt`. `ShareRequestService.Create` returns an existing canonical share when a submitted provider track is already represented, but the metrics query must still deduplicate by `SongId` defensively.
- MongoDB currently has no metrics snapshot collection or indexes for completed-source counts / recent completed requests. Public reads must not perform live aggregation.
- MassTransit scans the API assembly for consumers and uses the inbox/outbox middleware. Consumer definitions can bound concurrency, while repository compare-and-set logic is still required because multiple API replicas can consume refresh messages concurrently.
- `ServiceType` currently defines Spotify, Apple Music, and YouTube Music. Apple Music has no registered adapter today, but its zero count must still be present so the public contract automatically reflects all known non-`Unknown` enum values.
- The existing revalidation route accepts only validated 12-character hexadecimal share IDs. Metrics invalidation must extend that route with a fixed allowlisted target, never a caller-supplied path.
- CI requires `Backend Build & Test` and `Frontend Lint, Test & Build`; frontend production builds must tolerate the internal API being unavailable.

## Proposed implementation

### Persisted snapshot and bounded aggregation

Create a singleton `PublicMetricsSnapshot` MongoDB document containing:

- a stable singleton ID;
- total completed distinct-song count used as a monotonic snapshot version;
- generation timestamp;
- one count entry for every non-`Unknown` `ServiceType`;
- at most 20 recent embedded song entries with `SongId`, `ShareId`, title, artists, optional album/artwork, source service, and source request creation time.

Add repository query methods that count completed, song-backed share requests by source service and return the newest completed requests after grouping by `SongId`, with deterministic `CreatedAt` descending and `ShareId` tie-breaking. Fetch only the bounded song IDs needed to hydrate recent metadata. Add compound indexes supporting `(Status, SourceService)` counts and `(Status, CreatedAt desc, SongId)` recent selection.

`PublicMetricsService.RefreshAsync` will build the full candidate in the consumer process, fill zeroes for known services, and ask the snapshot repository to replace the singleton only when the candidate's distinct completed total is greater than or equal to the stored total. The repository will use a conditional Mongo update/insert with duplicate-key retry handling so a slower concurrent refresh cannot overwrite a newer snapshot. Duplicate messages are therefore safe. `GetAsync` returns a zero-count, empty-list response when no snapshot exists yet.

This performs one indexed, event-driven rebuild per completed canonical share. Public page requests only read cached HTML; the internal API only reads the small stored snapshot. No public route can initiate aggregation.

### Event and bootstrap flow

Add a `RefreshPublicMetrics` contract. After `CompleteSagaActivity` has updated song/share status and initiated the existing share-page revalidation, publish one `RefreshPublicMetrics` message. Do not publish from `SourceMetadataConsumer` or each service-link consumer; that would create partial snapshots and multiple rebuilds for a single share.

Add `PublicMetricsRefreshConsumer` with a consumer definition limiting prefetch/concurrency. It refreshes the persisted snapshot, then calls a dedicated `RevalidateMetricsAsync` method only when the candidate was accepted. Revalidation failures remain logged and non-fatal, matching current share-page behavior.

Add a non-blocking `BackgroundService` that creates the metrics indexes and publishes one refresh request when the API starts. Catch and log bootstrap failures so API availability is not tied to optional metrics initialization. Multiple replicas may bootstrap concurrently; the consumer and conditional repository write make that safe. This one-time event backfills existing production data without restoring the removed public reindex/revalidate-all feature.

### Internal read and static frontend

Add `GET /api/metrics` to the internal API. It returns only the stored snapshot DTO (or the safe empty DTO) and never rebuilds data. Do not extend `MusicShare.Frontend/src/proxy.ts`, so browsers cannot use the frontend origin as a live metrics API.

Add a server-rendered `/metrics` page with `revalidate = false`. It fetches the Aspire-internal API during static generation/revalidation, catches unavailable/non-success/invalid responses, and renders the safe empty model so deployment builds remain independent of API availability. The responsive page shows a card for every known platform count and at most 20 recent songs, each linked to `/share/{shareId}`. It must not use client polling, a refresh button, or browser-side fetching. Add a low-emphasis link from the home page to `/metrics`.

Change the frontend revalidation body to a strict union: either a validated `{ shareId }` request or the exact allowlisted `{ target: "metrics" }` request. Reject mixed, missing, or unknown targets. `IFrontendRevalidateService` exposes explicit `RevalidateShareAsync(shareId)` and `RevalidateMetricsAsync()` methods so application code cannot pass arbitrary paths.

## File-level plan

- `MusicShare.Contracts/Messages/RefreshPublicMetrics.cs`: define the refresh event contract.
- `MusicShare.Persistence/Entities/PublicMetricsSnapshot.cs`: add the singleton snapshot and embedded count/recent-song persistence models.
- `MusicShare.Persistence/IMusicShareDbContext.cs`: expose the snapshot collection.
- `MusicShare.Persistence/MusicShareDbContext.cs`: bind the `publicMetricsSnapshots` collection.
- `MusicShare.Persistence/Repositories/IShareRequestRepository.cs`: add completed source-count and recent distinct request query contracts.
- `MusicShare.Persistence/Repositories/ShareRequestRepository.cs`: implement indexed completed counts and deterministic distinct recent selection.
- `MusicShare.Persistence/Repositories/ISongRepository.cs`: add bounded bulk song lookup for recent results.
- `MusicShare.Persistence/Repositories/SongRepository.cs`: implement bulk lookup without per-song round trips.
- `MusicShare.Persistence/Repositories/IPublicMetricsSnapshotRepository.cs`: define singleton read and conditional non-regressing replace operations.
- `MusicShare.Persistence/Repositories/PublicMetricsSnapshotRepository.cs`: implement safe insert/compare-and-set replacement and duplicate-upsert race handling.
- `MusicShare.Persistence/DependencyInjection.cs`: register the snapshot repository.
- `MusicShare.Services/Models/PublicMetricsResponse.cs`: define the internal API/service response and recent-song/count DTOs.
- `MusicShare.Services/Services/IPublicMetricsService.cs`: define safe read and refresh operations.
- `MusicShare.Services/Services/PublicMetricsService.cs`: aggregate authoritative repository data into a bounded snapshot, include zero-valued known services, and return an empty response before bootstrap.
- `MusicShare.Services/Services/IFrontendRevalidateService.cs`: replace the generic request method with explicit share and metrics revalidation methods.
- `MusicShare.Services/Services/FrontendRevalidateService.cs`: send strict share or fixed metrics payloads to the existing frontend route.
- `MusicShare.Services/DependencyInjection.cs`: register the metrics service and updated revalidation client.
- `MusicShare.Api/Consumers/PublicMetricsRefreshConsumer.cs`: rebuild/persist the snapshot and request `/metrics` invalidation after an accepted refresh.
- `MusicShare.Api/Consumers/PublicMetricsRefreshConsumerDefinition.cs`: bound refresh endpoint prefetch/concurrency and apply a small transient retry policy without introducing parallel rebuilds in one replica.
- `MusicShare.Api/Services/PublicMetricsBootstrapService.cs`: asynchronously create the two metrics query indexes and publish startup refresh without blocking or crashing API startup.
- `MusicShare.Api/Sagas/ShareRequest/Activities/CompleteSagaActivity.cs`: publish one metrics refresh event after the workflow is finalized while preserving share-page revalidation.
- `MusicShare.Api/Controllers/MetricsController.cs`: expose the stored snapshot through the internal API boundary only.
- `MusicShare.Api/Program.cs`: register the bootstrap hosted service.
- `MusicShare.Frontend/src/app/api/revalidate/route.ts`: authenticate, parse the strict request union, validate share IDs, and invalidate only `/share/{id}` or `/metrics`.
- `MusicShare.Frontend/src/app/metrics/page.tsx`: add the indefinitely cached server page, internal snapshot fetch, responsive counts/recent list, canonical links, metadata, and safe empty/error state.
- `MusicShare.Frontend/src/app/page.tsx`: add a secondary link to the public metrics page.
- `MusicShare.Frontend/src/lib/api.ts`: add shared public metrics TypeScript types if needed by the page/tests; do not add a browser metrics client.
- `MusicShare.Tests/Unit/Persistence/*`: cover completed-only source counts, distinct/deterministic/bounded recent selection, bulk song lookup, and non-regressing snapshot writes using repository seams or focused mocks appropriate to the current test architecture.
- `MusicShare.Tests/Unit/Services/PublicMetricsServiceTests.cs`: cover zero counts, empty snapshot reads, aggregation mapping, deduplication, bounds, and accepted/rejected candidate behavior.
- `MusicShare.Tests/Unit/Api/Consumers/PublicMetricsRefreshConsumerTests.cs`: cover accepted refresh + revalidation, stale/no-op refresh, and failure propagation/retry semantics.
- `MusicShare.Tests/Unit/Api/Services/PublicMetricsBootstrapServiceTests.cs`: cover index creation/publication and logged non-fatal bootstrap errors.
- `MusicShare.Tests/Unit/Api/Sagas/CompleteSagaActivityTests.cs`: verify exactly one metrics refresh publication after successful completion paths and update existing revalidation assertions.
- `MusicShare.Tests/Unit/Api/Controllers/MetricsControllerTests.cs`: cover stored and empty snapshot responses.
- `MusicShare.Tests/Unit/Services/FrontendRevalidateServiceTests.cs`: cover exact share and metrics request bodies plus existing failure tolerance.
- `MusicShare.Tests/Unit/Services/AddFrontendRevalidateServiceTests.cs`: update DI/client method calls without weakening API-key/base-address checks.
- `MusicShare.Frontend/src/app/api/revalidate/route.test.ts`: cover strict target allowlisting, mixed/malformed bodies, auth, existing share IDs, and exact `/metrics` invalidation.
- `MusicShare.Frontend/src/app/metrics/page.test.tsx`: cover platform counts including zero, newest-first canonical links, 20-item bound, metadata content, empty state, and failed internal fetch behavior.
- `MusicShare.Frontend/src/app/page.test.tsx`: verify the new metrics link without changing the existing hierarchy/footer behavior.

The worker may consolidate closely related model files or test files when that better matches existing conventions, but must preserve each responsibility and boundary above.

## Validation plan

- `git diff --check`
- `dotnet build MusicShare.slnx`
- `dotnet test MusicShare.Tests/MusicShare.Tests.csproj`
- `cd MusicShare.Frontend && npm run lint`
- `cd MusicShare.Frontend && npm test`
- `cd MusicShare.Frontend && npm run build`
- Inspect the Next.js build route table to confirm `/metrics` is static/ISR and does not become a per-request dynamic page.
- Verify `MusicShare.Frontend/src/proxy.ts` still matches only `/api/share/:path*`.
- Review tests/implementation to confirm only the fixed `/metrics` path can be invalidated and only the consumer invokes metrics aggregation.

## Risks and edge cases

- Existing production rows predate the feature. Startup bootstrap must rebuild from all completed, song-backed requests before invalidating `/metrics`; until then the page safely shows zero counts and an empty recent list.
- API replicas can start and consume concurrently. The snapshot version/conditional write must prevent a smaller or older candidate from replacing a newer one; consumer concurrency settings alone are insufficient across replicas.
- MassTransit may redeliver messages. Refresh is idempotent because it recomputes from authoritative state and conditionally replaces one singleton document.
- A share may be pending, failed before metadata, or missing its song. Exclude it from counts and recent entries. Partially resolved songs are included once the share itself is completed because their canonical result page is stable.
- Historic/corrupt duplicate share requests may reference the same song. Group by `SongId` and choose the newest canonical request before counting and selecting recent items so both the total and recent list are distinct songs.
- `ServiceType.Unknown` must never appear publicly. Newly added real enum values should appear automatically with a zero count before any songs exist.
- Equal timestamps require deterministic `ShareId` ordering. The frontend should preserve backend order.
- The API may be unavailable during `next build` or revalidation. The page must not fail the build and must render the safe empty state.
- A frontend revalidation call can fail after the snapshot is stored. Do not roll back the snapshot or fail the completed share; the next bootstrap/share refresh can retry invalidation.
- The metrics snapshot is intentionally eventual, not real-time. No client polling or public manual refresh is introduced.

## Definition of done

- [ ] A public, responsive `/metrics` page displays all known source-service counts and at most 20 newest distinct completed songs.
- [ ] Every recent item links to its canonical MusicShare `/share/{shareId}` page and no external provider URL is used as the canonical list link.
- [ ] `/metrics` is indefinitely cached/static between authenticated on-demand invalidations and contains no client polling or manual rebuild control.
- [ ] Browser requests cannot invoke aggregation, access the internal metrics API through the frontend proxy, or choose an arbitrary revalidation path.
- [ ] Saga completion publishes exactly one metrics refresh request after statuses and service-link attempts are stable.
- [ ] The refresh consumer rebuilds and persists a bounded snapshot, tolerates duplicate/concurrent messages without regression, and invalidates `/metrics` only after an accepted write.
- [ ] Startup bootstrap safely creates query indexes and requests a backfill for existing data without making API startup depend on success.
- [ ] Pending/failed-without-song requests and duplicate song references do not inflate counts or the recent list; partial successes with completed share pages are included.
- [ ] Missing snapshot/API data renders a safe empty state and frontend builds do not require a running backend.
- [ ] Focused backend and frontend tests cover security, idempotency/concurrency, aggregation, rendering, and empty/failure behavior.
- [ ] `git diff --check`, backend build/tests, and frontend lint/tests/build all pass.
