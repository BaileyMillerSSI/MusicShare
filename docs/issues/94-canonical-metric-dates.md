# Issue #94: Use canonical song dates and clarify weekly metric labels

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/94
- Branch: `issue/94-canonical-metric-dates`
- Status: Approved for implementation

## Request

Correct the date semantics and presentation behind the public `/metrics` weekly chart. Songs shared on 8/31 were already present in the current `+9` bucket, but the final bar displayed `08/29, 8:00 PM EDT`, making the bucket-start instant look like a stale latest-activity date. Use the canonical persisted song creation date for added-song chronology and present weekly buckets as weeks rather than isolated timestamps.

## Repository findings

- The live `/metrics` snapshot generated at `2026-09-01T01:12:14.758Z` reported 292 songs and `+9 this week`. In America/New_York that generation time was approximately 9:12 PM on 8/31, confirming the current snapshot included activity from the user's local 8/31.
- The apparent `08/29, 8:00 PM EDT` cutoff is `2026-08-30T00:00:00Z`, the current Sunday UTC week boundary converted to Eastern time. It is not the newest song timestamp.
- `MusicShare.Persistence/Entities/Song.cs` already persists canonical `CreatedAt`. `SongRepository.InsertAsync` sets it when the canonical song is created after source metadata resolution.
- `ShareRequestRepository.GetCompletedDistinctSongCountsByWeekAsync` currently groups eligible completed shares by song and chooses the earliest `ShareRequest.CreatedAt`; `GetRecentCompletedDistinctAsync` chooses the latest completed share timestamp. Neither uses `Song.CreatedAt`, so the weekly and recent “added” chronology is tied to request records instead of the canonical song.
- The share-request repository has access to the database context and can use the actual songs collection name for a MongoDB `$lookup`, matching the established lookup pattern in `SongServiceLinkRepository`. The song `_id` index supports the join; no new collection or API field is required.
- An eligible completed public share must remain the publication gate. Canonical song lookup changes the timestamp source, not which pending/failed/private data becomes public.
- `WeeklyCompletedSongsChart` is a hydration-safe client component: SSR and initial hydration use deterministic UTC labels, then a post-mount effect uses browser-local formatting. Its current local formatter emits a single start timestamp with hour/minute/time-zone text, which stacks awkwardly at the supplied narrow mobile viewport and reads as an activity cutoff.
- The frontend snapshot already provides ordered `weekStart` instants and counts. The final element is the current partial UTC bucket, so it can be labeled `This week` without browser clock inference or API changes.

## Proposed implementation

Extend the share-request metrics pipeline with a canonical-song lookup stage. Start from the existing completed/public/deduplicated share rows so each song remains eligible only once and retains a canonical share link/source. Join the real songs collection by ObjectId, require exactly a valid song `createdAt` date, replace the aggregate's chronology field with that canonical value, and reuse the result for both recent ordering/materialization and weekly range bucketing. The weekly `$dateTrunc` and its in-memory compatibility fallback continue to use Sunday UTC boundaries, but operate on `Song.CreatedAt`. Orphaned/malformed song rows disappear from canonical-date weekly/recent results. The total/link metrics and response shape remain untouched.

Revise the chart labels to communicate intervals. During SSR and initial hydration, historical buckets render compact deterministic Sunday-through-Saturday UTC date ranges. After mount, historical ranges switch to the local calendar dates covering the same UTC interval, using date-only visible text so no isolated `8:00 PM` appears beneath a bar. The final bucket renders `This week` in both phases. Add deterministic explanatory copy that the aggregation uses Sunday UTC boundaries and displayed historical ranges adapt to local time. Accessible labels retain fuller start/end instants, count, and UTC-boundary context. Preserve the existing post-effect transition, formatter fallback, ISO `<time dateTime>`, stable keys, counts, and bar geometry; do not use hydration suppression.

## File-level plan

- `MusicShare.Persistence/Repositories/ShareRequestRepository.cs`: retain the completed/public distinct-song gate, add/reuse a canonical `Song.CreatedAt` lookup pipeline based on the actual songs collection name, use it for recent results and weekly aggregation/fallback, and defensively exclude missing/malformed canonical dates.
- `MusicShare.Tests/Unit/Persistence/ShareRequestRepositoryMetricsTests.cs`: replace earliest-request pipeline expectations with canonical song-lookup/date replacement assertions and retain defensive materialization coverage.
- `MusicShare.Tests/Integration/PublicMetricsMongoRepositoryTests.cs`: seed canonical song rows separately from share rows and prove weekly assignment/recent ordering follow `Song.CreatedAt` across Sunday boundaries even when request dates disagree; cover duplicate completed shares plus orphan/malformed songs and update affected fixtures.
- `MusicShare.Frontend/src/app/metrics/WeeklyCompletedSongsChart.tsx`: render historical date ranges, label the final bucket `This week`, add concise UTC/local explanatory copy, preserve hydration-safe localization/fallback, and keep mobile labels compact.
- `MusicShare.Frontend/src/app/metrics/WeeklyCompletedSongsChart.test.tsx`: cover deterministic UTC server ranges, post-mount local ranges, current-bucket labeling, local prior-day boundaries, counts/heights/ISO identity, formatter failure, explanatory/accessibility copy, and hydration diagnostics.
- `MusicShare.Frontend/src/app/metrics/page.test.tsx`: update page integration assertions for the revised chart semantics while retaining request-time rendering, metadata, fallback, count, and recent-link coverage.

## Validation plan

- Run focused backend tests for `ShareRequestRepositoryMetricsTests` and `PublicMetricsMongoRepositoryTests`.
- Run `dotnet test MusicShare.Tests/MusicShare.Tests.csproj` and `dotnet build MusicShare.slnx --configuration Release`.
- From `MusicShare.Frontend`, run focused Vitest coverage for `WeeklyCompletedSongsChart.test.tsx` and `page.test.tsx`, then full `npm test`, `npm run lint`, and `npm run build`.
- Confirm the frontend production build still reports `/metrics` as dynamic/request-time rendered and no backend metrics route becomes public.
- Run the implementation against a controlled snapshot at a narrow mobile viewport comparable to the supplied screenshot. Confirm the final bar says `This week`, historical labels communicate compact ranges, the explanatory copy is readable, and page/chart `scrollWidth` does not exceed the viewport.
- Run `git diff --check` and confirm the branch contains only issue-scoped changes.

## Risks and edge cases

- `Song.CreatedAt` marks canonical song creation after source metadata resolution, not request submission or saga completion. It is nevertheless the existing authoritative song-level creation field and the correct source for “added” chronology; no separate `CompletedAt` currently exists.
- Canonical creation may cross a Sunday UTC boundary relative to request submission. The song must move to its canonical creation week even if this changes an existing bucket after refresh.
- Multiple completed requests can point at one song. Deduplicate before the song lookup and retain one canonical share link/source while using only the song timestamp for chronology.
- Old or malformed rows may have a completed share but no matching song or no valid song date. Excluding them from weekly/recent chronology avoids fabricating a fallback date; total/link behavior remains unchanged by scope.
- A Sunday UTC interval can begin Saturday evening in zones west of UTC. Historical labels should therefore be ranges and the current bucket should say `This week`; explanatory and accessible text must preserve the UTC-boundary truth without implying local re-bucketing.
- Locale output varies. Exact frontend tests must control `Intl.DateTimeFormat`, while browser QA checks the real narrow presentation.
- MongoDB compatibility fallback must reuse the canonical-song lookup before in-memory Sunday grouping; falling back to request dates would silently restore the bug.
- The current snapshot will be corrected by the normal startup/event refresh and existing versioned replacement flow after deployment; no migration or public refresh endpoint is needed.

## Definition of done

- [ ] Eligible completed-song weekly counts and recent ordering/materialization use canonical `Song.CreatedAt` rather than any `ShareRequest.CreatedAt`.
- [ ] Duplicate completed shares count once; canonical dates determine Sunday UTC bucket placement even when request and song dates cross a boundary.
- [ ] Missing/malformed canonical song rows do not appear in weekly/recent chronology, and the MongoDB fallback uses the same canonical date semantics.
- [ ] The current partial bucket visibly says `This week`; historical buckets render compact UTC ranges on the server and local ranges after hydration, without a misleading isolated timestamp.
- [ ] Explanatory and accessible copy clearly distinguishes Sunday UTC aggregation boundaries from viewer-local display ranges.
- [ ] Server markup and the first client render remain identical, post-mount localization produces no hydration diagnostics, and no hydration-warning suppression is added.
- [ ] Existing total/link counts, snapshot/API contract, scheduler, private/no-polling boundary, dynamic route, metadata/share image, count/bar geometry, and safe empty behavior remain unchanged.
- [ ] Focused/full backend and frontend tests, Release build, frontend production build/lint, narrow mobile rendered QA, and `git diff --check` pass.
