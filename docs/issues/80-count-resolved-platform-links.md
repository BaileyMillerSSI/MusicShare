# Issue #80: Count resolved platform links in public metrics

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/80
- Branch: `issue/80-count-resolved-platform-links`
- Status: Approved for implementation

## Request

Clean up the public metrics page by removing Apple Music from this metrics surface and correcting the provider totals to count the distinct Spotify and YouTube Music links available on completed songs. The production share `d3c6e72e9c9a` is the concrete acceptance example: its completed result contains one Spotify link and one YouTube Music link, so it must contribute one completed song, one Spotify link, and one YouTube Music link.

## Repository findings

- Production and the public `GET /api/share/d3c6e72e9c9a` response confirm that the example song has `serviceType` 1 (Spotify) and 3 (YouTube Music), while the cached `/metrics` page currently reports 289 Spotify source submissions, 0 Apple Music sources, and 0 YouTube Music sources.
- `PublicMetricsService.RefreshAsync` currently derives both `TotalCompletedSongs` and `ServiceCounts` from `ShareRequestRepository.GetCompletedDistinctSongCountsBySourceAsync`. This measures the provider originally submitted, not the links available in `songServiceLinks`, which explains the misleading YouTube zero.
- `SongServiceLink` is the authoritative persisted representation of an available provider link and contains `SongId` plus `ServiceType`. Link consumers finish before `CompleteSagaActivity` marks the share completed and publishes `RefreshPublicMetrics`, so the existing event boundary remains correct.
- A completed song can have multiple provider links, so link counts cannot be summed to derive the distinct-song total. The total must remain an independent distinct completed `ShareRequest.SongId` count.
- Historic or malformed data can contain duplicate share requests or duplicate song/service link rows. The existing share aggregation groups by song; the link aggregation must likewise group by `(SongId, ServiceType)` and require at least one completed, song-backed share before counting the pair.
- The persisted snapshot and API DTO already use the neutral `ServiceCounts` name. Snapshot versions are database-reserved, so a startup or event refresh can replace the currently persisted source counts even when the completed-song total is unchanged.
- Apple Music remains in the shared `ServiceType`, URL parsing, and frontend share-result enum, but no Apple adapter is registered. Removing that cross-application scaffolding is outside this issue; only metrics defaults, displayed cards/copy, and aggregation output should be limited to Spotify and YouTube Music.
- The metrics page currently renders source-provider text beside every recent song. That detail is not needed for the recent canonical list and would preserve an Apple-specific rendering path, so the recent rows should show artist metadata without a source-provider suffix.
- `/metrics` is statically generated with `revalidate = false`, refreshed through the authenticated fixed metrics target, and backed by the stored internal snapshot. This issue must not change those security, caching, networking, or event-driven boundaries.
- CI requires `Backend Build & Test` and `Frontend Lint, Test & Build`. The existing validation commands are the solution build/tests and frontend lint/tests/build.

## Proposed implementation

Separate the completed-song total from the provider-link counts:

1. Replace the source-grouped metrics query on `IShareRequestRepository` with a distinct completed-song count query that reuses the existing completed, valid-song aggregation stages.
2. Add a metrics aggregation to `ISongServiceLinkRepository`/`SongServiceLinkRepository`. It will filter valid link rows, deduplicate by `(songId, serviceType)`, use the actual share-request collection name from the database context in a MongoDB lookup, require a matching completed share for the song, and group the remaining pairs by service. This makes duplicate link rows harmless and excludes links that belong only to non-public/pending data.
3. Have `PublicMetricsService` obtain the total from the share-request repository and provider counts from the link repository. Define the metrics platform list once as Spotify and YouTube Music, and use that list for both refreshed snapshots and the safe empty response. Ignore Apple Music, `Unknown`, undefined values, and any other non-displayed service in the metrics response.
4. Update metrics bootstrap indexes for the new query shapes: retain the recent completed-share ordering index, replace the obsolete source-count index with a completed-song lookup index, and add a link `(ServiceType, SongId)` index.
5. Present three explicit summary cards on `/metrics`: completed songs, Spotify links, and YouTube Music links. Remove Apple Music from page constants, labels, fallback counts, metadata/copy, accessibility labels, and tests. Remove the source-service suffix from recent song rows while keeping their existing canonical links and order.

The snapshot/event/ISR pipeline stays intact. API startup will rebuild the stored snapshot using the corrected semantics and the accepted refresh will invalidate the cached page.

## File-level plan

- `MusicShare.Persistence/Repositories/IShareRequestRepository.cs`: replace the source-grouped metrics method with an independent distinct completed-song count contract.
- `MusicShare.Persistence/Repositories/ShareRequestRepository.cs`: implement the distinct total using the existing completed/deduplicated pipeline and remove source-count materialization that is no longer used.
- `MusicShare.Persistence/Repositories/ISongServiceLinkRepository.cs`: add the completed distinct-song link-count contract.
- `MusicShare.Persistence/Repositories/SongServiceLinkRepository.cs`: aggregate distinct `(SongId, ServiceType)` pairs with a completed-share existence lookup and materialize only defined, non-`Unknown` services.
- `MusicShare.Services/Models/PublicMetricsResponse.cs`: define/reuse the Spotify-and-YouTube metrics platform list for the safe empty response without changing shared `MusicServiceType` behavior elsewhere.
- `MusicShare.Services/Services/PublicMetricsService.cs`: inject the link repository, keep total songs independent, populate only Spotify/YouTube link counts, and preserve recent-song hydration, bounds, snapshot versioning, and replace behavior.
- `MusicShare.Api/Services/PublicMetricsBootstrapService.cs`: create indexes supporting completed-song lookup, recent completed ordering, and per-service link aggregation before publishing the startup refresh.
- `MusicShare.Frontend/src/app/metrics/page.tsx`: render completed songs, Spotify links, and YouTube Music links; remove Apple Music and source-count wording; show recent artists without a source-provider suffix; preserve static fetching, validation, canonical links, responsiveness, and empty/failure behavior.
- `MusicShare.Tests/Unit/Persistence/ShareRequestRepositoryMetricsTests.cs`: update pipeline/count expectations for the independent distinct-song total.
- `MusicShare.Tests/Unit/Persistence/SongServiceLinkRepositoryMetricsTests.cs`: cover link aggregation construction/materialization, valid service handling, and malformed rows where useful.
- `MusicShare.Tests/Integration/PublicMetricsMongoRepositoryTests.cs`: seed completed/pending requests and Spotify/YouTube/duplicate/orphan link rows; prove one completed dual-linked song counts once in each provider, duplicates do not inflate, and non-public links are excluded. Preserve concurrency/snapshot integration coverage and update test repository fakes for the interface change.
- `MusicShare.Tests/Unit/Services/PublicMetricsServiceTests.cs`: cover independent total/link counts, only Spotify/YouTube defaults/output, the dual-link example, recent bounds, canonical IDs, and unknown/Apple filtering.
- `MusicShare.Tests/Unit/Api/Services/PublicMetricsBootstrapServiceTests.cs`: update index assertions for both collections and the new query shapes.
- `MusicShare.Frontend/src/app/metrics/page.test.tsx`: assert the three corrected metrics, absence of Apple Music, link-oriented copy/accessibility, canonical recent links/order/bounds, and safe empty/error behavior.
- Any directly affected mocks/fakes implementing `IShareRequestRepository` or constructing `PublicMetricsService`: update signatures and injected dependencies without unrelated refactors.

## Validation plan

- `git diff --check`
- `dotnet build MusicShare.slnx`
- `dotnet test MusicShare.Tests/MusicShare.Tests.csproj`
- `cd MusicShare.Frontend && npm run lint`
- `cd MusicShare.Frontend && npm test`
- `cd MusicShare.Frontend && npm run build`
- Inspect the Next.js route table to confirm `/metrics` remains static and inspect `MusicShare.Frontend/src/proxy.ts` to confirm the internal metrics API is still not publicly proxied.
- Inspect the rendered/tested metrics page to confirm there is no Apple Music card or source-count wording and the example semantics are represented as one song plus one link for each resolved platform.

## Risks and edge cases

- MongoDB stores enum values as strings and song IDs as ObjectIds. The link aggregation must validate those BSON shapes and use the context's actual collection namespace so production and isolated integration-test collection names both work.
- Links can exist before a share is completed. A raw link collection group would leak pending work into public totals; the completed-share lookup is required.
- Multiple historic shares can reference one song and duplicate links can reference one song/service. Both totals must be distinct by song, with the link total distinct by song and service.
- A completed song may have only one resolved provider link. It still contributes one to the song total and only to the platform links actually stored.
- The total song count and individual link counts intentionally differ and link counts intentionally overlap; a two-platform song contributes to both provider counts but only once to the total.
- Existing persisted source-count snapshots may be served until the post-deploy bootstrap refresh is accepted and the metrics page is invalidated. The current version reservation/replace flow supports same-total corrections and must remain intact.
- Shared Apple Music types/components are used by non-metrics share parsing/rendering tests. Removing them would broaden scope and risk regressions, so they must remain unchanged.
- Frontend build-time API unavailability must continue to produce the safe zero-valued two-platform metrics response instead of failing the build.

## Definition of done

- [ ] The example completed song semantics are covered: one song with Spotify and YouTube Music links contributes one completed song, one Spotify link, and one YouTube Music link.
- [ ] Provider totals count distinct completed songs with a stored link for that service, not original submission sources.
- [ ] Duplicate song/service links, duplicate shares, pending/non-public songs, malformed rows, unknown services, and Apple Music do not inflate or appear in public metrics counts.
- [ ] `/metrics` displays exactly the completed-song, Spotify-link, and YouTube Music-link summaries and contains no visible Apple Music or source-count wording.
- [ ] Recent songs remain newest-first, capped at 20, and linked to canonical `/share/{shareId}` pages without an Apple/source-provider rendering path.
- [ ] Persisted snapshots, event-driven refresh, startup backfill, authenticated exact-path invalidation, internal API isolation, and indefinite static caching remain intact.
- [ ] Focused backend/integration/frontend tests cover the corrected aggregation and rendering semantics.
- [ ] `git diff --check`, backend build/tests, and frontend lint/tests/build all pass.
