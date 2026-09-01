# Issue #97: Prevent concurrent duplicate shares and add safe reconciliation

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/97
- Branch: `issue/97-prevent-concurrent-duplicate-shares`
- Status: Approved for implementation

## Request

Find the defect that allowed `https://music.baileymiller.dev/share/7c9ff089371a` and `https://music.baileymiller.dev/share/15586c2697d3` to exist as separate public shares for the same song. Plan and document, but do not implement in this task, a race-safe fix and an application-managed way to reconcile these production duplicates without directly editing MongoDB.

This delivery intentionally stops after the GitHub issue and committed plan. No application code, repair execution, deployment, or production data mutation is authorized in this task.

## Repository findings

- Both live pages render “Chicago” by Michael Jackson from `XSCAPE`, duration `4:05`, and expose the same Spotify track `5BKKy9fIJL5uM9fz1SnqyP` and YouTube Music video `wAoq__SQpwk`.
- The public API responses contain different Song IDs, `6a919d4fed75110614281818` and `6a919d4fed75110614281819`. These Mongo ObjectIds are consecutive and share the embedded timestamp `2026-08-28T14:38:07Z`, which is strong evidence that overlapping submissions created two songs in the same second. Both responses use Spotify CDN artwork, consistent with the same Spotify source track being resolved twice.
- The public `/metrics` page also lists the two Chicago share IDs as adjacent recent songs, proving that this is not only an alternate URL for one canonical Song record. Other adjacent repeated titles are visible, so existing production data may contain more duplicate pairs; title equality alone is not safe proof that those records are the same track.
- `ShareRequestService.Create` extracts the canonical provider track ID, but its only duplicate lookup is `SongServiceLinkRepository.GetByServiceAndSongIdAsync`. A `SongServiceLink` is created later by `SourceMetadataConsumer`, after the request has been inserted and `SongShareSubmitted` has started the asynchronous saga. During that gap, every overlapping submission observes no link, creates its own `ShareRequest`, and publishes its own workflow.
- `IShareRequestRepository.GetByServiceTrackIdAsync` and its Mongo implementation still exist. Commit `f078649` originally used this lookup to return an in-flight request; commit `86ffedb` replaced it with the later link-based lookup to support cross-provider reuse. That replacement reopened the pending/processing gap. Restoring the read alone would reduce the window but would still be a check-then-insert race when two requests arrive together.
- `SourceMetadataConsumer` unconditionally inserts a new `Song` and source `SongServiceLink` for every accepted workflow. Once duplicate workflows have been published, there is no later canonicalization step, database uniqueness rule, or alias model that makes them converge.
- `ShareRequest` already stores `SourceService`, `ServiceTrackId`, `CreatedAt`, and `SongId`, so the same-provider identity required for an atomic reservation is available before provider metadata work begins.
- Mongo index bootstrap exists, but `PublicMetricsBootstrapService` creates only metrics indexes; no unique share identity index exists. A unique compound index on the existing `(SourceService, ServiceTrackId)` fields would fail to deploy while historical duplicates remain. A new sparse identity field lets new writes become race-safe without first editing old data.
- Public metrics deduplicate only by `SongId`. Because the two requests point to two different Song records, both are counted and rendered. Reconciled aliases must be excluded before the existing distinct-song aggregation and link-count joins.
- `ShareResultResponse` returns the resolved record's `shareId`; the server-rendered Next.js share page can compare that value with the requested route ID and use `permanentRedirect` to preserve old URLs while establishing one canonical public URL.
- The API is private behind Aspire and only the Next.js frontend has public ingress. The removed re-indexing implementation previously used broad internal/frontend maintenance routes and exposed full ID lists. Issue #62 deliberately removed that incomplete surface. The replacement must be narrowly scoped to an explicit pair, authenticated, validated, dry-run first, and minimally disclosive.
- The existing Next.js `/api/revalidate` route and `FrontendRevalidateService` provide the established secret-backed API-to-frontend pattern for invalidating a bounded share path and `/metrics`. Reconciliation can reuse the revalidation service while using a separate maintenance secret and route.
- CI already provides a real MongoDB 8.0 service through `MUSICSHARE_TEST_MONGODB`. The concurrency guarantee must be proven there; mock-only unit tests cannot validate a unique-index race.
- The current primary checkout contains unrelated uncommitted metrics work. This issue branch and plan were created from fetched `origin/main` in an isolated worktree so none of that work is mixed into this plan.

## Proposed implementation

### 1. Make same-provider submission reservation atomic

Add nullable `SourceIdentityKey` to `ShareRequest`, serialized with `BsonIgnoreIfNull`. Build it only when the adapter returns a validated canonical track ID, using a stable versioned format such as `v1:{numeric ServiceType}:{ServiceTrackId}`. New canonical requests carry the key; historical records remain unchanged until an explicit reconciliation.

Create a startup initializer that blocks API startup until MongoDB has a named unique sparse index on `ShareRequest.SourceIdentityKey`. It must fail startup if the correctness index cannot be created rather than silently serving a racy write path. The new field avoids index conflicts from historical duplicate `(SourceService, ServiceTrackId)` rows because those rows do not contain `SourceIdentityKey`.

Replace the create path's generic insert with `ReserveBySourceIdentityAsync`, returning both the selected request and whether the caller inserted it. The repository attempts one insert. If Mongo reports duplicate key for the named identity index, it reloads and returns the winning request; unrelated Mongo write errors propagate. `ShareRequestService.Create` keeps the completed cross-provider link lookup first, resolves any returned alias to its terminal canonical share, then performs the atomic reservation. Only the caller whose insert succeeds publishes `SongShareSubmitted`; losing callers return the winner's share ID without publishing another workflow. Inputs without a provider track ID retain the existing non-deduplicated behavior because they have no trustworthy identity.

This change guarantees convergence for the demonstrated same-Spotify-track race across pending, processing, and completed states. Existing link-based reuse continues to cover already-resolved submissions from another provider. Concurrent submissions that begin from different providers are a separate identity-resolution problem: do not claim that this issue fully merges them unless an exact shared platform identity is later proven by the reconciliation service.

### 2. Represent reconciliation as a non-destructive alias

Add nullable `CanonicalShareId`, `ReconciledAt`, and `ReconciliationId` fields to `ShareRequest`, all ignored when null. A canonical request has no `CanonicalShareId`; an alias points directly to one terminal canonical request. Do not create alias chains and do not delete ShareRequest, Song, SongServiceLink, or saga documents.

Add compare-and-set repository operations that load both candidate shares and their songs/links, mark the alias only if its current reconciliation state still matches the dry-run expectation, and optionally backfill `SourceIdentityKey` onto the canonical request. Retrying the same operation returns success with `changed: false`; attempts to remap an alias, create a cycle, target a missing/non-completed share, or claim an identity owned by a third record fail closed.

`ShareRequestService.GetByShareIdAsync` must follow at most one validated alias hop and materialize the canonical request/song while returning the canonical `ShareId`. Submission dedupe lookups must also resolve aliases before returning an ID.

Filter alias rows out of the completed-share aggregation before grouping by `SongId`, and out of the completed-song ID set used by service-link metrics. The historical duplicate Song and link documents remain recoverable but cease to affect the public canonical view.

### 3. Add a narrow reconciliation domain operation

Add `IDuplicateShareReconciliationService` with one operation that accepts exactly two validated 12-character lowercase hexadecimal share IDs, an optional canonical selection, and `DryRun` or `Apply` mode.

The service loads both requests, songs, and links; converts each link to the exact identity `(ServiceType, ServiceSongId)`; and requires at least one identical provider identity shared by both songs. Metadata similarity, artwork, duration, title, album, or artist names are supporting display information only and can never authorize a merge. Requests must be completed, distinct, non-conflicting, and resolve to valid songs. Unless the caller explicitly selects one of the validated pair, choose the earliest `ShareRequest.CreatedAt`, then lowercase `ShareId` as the deterministic tie-breaker.

Dry-run returns a bounded plan containing an operation fingerprint/ID, selected canonical share, proposed alias, shared exact identity evidence, current states, and affected counts. It performs no writes, revalidation, or metrics refresh. Apply requires the dry-run operation fingerprint so the compare-and-set rejects stale data; it writes the alias metadata, backfills the canonical source identity when safe, revalidates both share paths, and publishes one `RefreshPublicMetrics` message. Log the operation ID, mode, IDs, exact service types, outcome, and `changed` state without logging secrets, source URLs, or arbitrary document contents.

### 4. Expose an operator workflow without exposing MongoDB

Add a dedicated `Maintenance` shared secret in AppHost and production deployment configuration. Protect a narrowly routed backend command such as `POST /internal/maintenance/duplicate-shares/reconcile` with constant-time secret comparison and a fail-closed `503` when the secret is absent. Do not add this route to the general `/api/share/:path*` proxy and do not restore list-all, re-index-all, arbitrary-query, or delete endpoints.

Add an explicit Next.js server route at `/api/maintenance/duplicate-shares` that is the only public ingress for the command. It authenticates with the same dedicated maintenance secret, independently validates the exact JSON shape/IDs/mode/fingerprint, forwards only to the fixed Aspire-internal backend URL, applies a bounded timeout, and returns only the bounded reconciliation result. Use the dedicated secret rather than the existing revalidation secret so operator authority can be rotated independently.

Add a manual-only GitHub Actions workflow with production-environment protection and inputs for the two share IDs, optional canonical ID, mode (`dry-run` default or `apply`), and dry-run fingerprint. The workflow calls the fixed frontend route with the repository secret, never prints the secret, uploads or summarizes the bounded JSON result, and refuses apply without the fingerprint plus an explicit confirmation value. This gives the owner an audited “Run workflow” path and avoids shelling into MongoDB or handling a Mongo connection string.

### 5. Preserve canonical public URLs

Extend the TypeScript response type to acknowledge that the API may return a canonical `shareId` different from the requested alias. In the server share page, validate both IDs and call `permanentRedirect('/share/{canonicalId}')` before rendering or starting the client poller. Metadata generation should emit the canonical share URL in `alternates.canonical` and `openGraph.url`; an alias request must use the returned canonical ID. Reconciliation revalidates both cached paths so the alias begins redirecting immediately and the canonical page reflects the authoritative data.

## Planned operator workflow

The future production runbook should use these steps; none are executed by this planning task:

1. Open the manual `Reconcile duplicate shares` GitHub Actions workflow.
2. Enter `7c9ff089371a` and `15586c2697d3`, leave mode as `dry-run`, and run it.
3. Verify the response proves shared Spotify identity `5BKKy9fIJL5uM9fz1SnqyP` and shared YouTube Music identity `wAoq__SQpwk`, proposes `7c9ff089371a` as the earlier canonical share, proposes `15586c2697d3` as its alias, and reports no mutations.
4. Copy the dry-run fingerprint into a second workflow run, select `apply`, and enter the exact required confirmation value. Production-environment approval remains a separate human gate when configured.
5. Confirm the apply result reports one alias change, both share paths revalidated, and a metrics refresh queued. A repeated apply must report `changed: false` and remain successful.
6. Verify `https://music.baileymiller.dev/share/15586c2697d3` permanently redirects to `https://music.baileymiller.dev/share/7c9ff089371a`, the canonical page still renders Chicago and both platform links, and `/metrics` contains one Chicago entry/count contribution after refresh.
7. Retain the GitHub Actions run URL and reconciliation operation ID as the audit record. Do not delete the duplicate database documents or edit MongoDB manually.

The canonical choice above is based on the consecutive Song ObjectId order available through the public API. The implemented dry-run must re-evaluate authoritative `ShareRequest.CreatedAt` values and fail if they contradict the proposed ordering or if the exact provider identities do not match.

## File-level plan

- `MusicShare.Persistence/Entities/ShareRequest.cs`: add the nullable source identity and alias/reconciliation fields with serialization attributes that keep them absent until explicitly set.
- `MusicShare.Persistence/Repositories/IShareRequestRepository.cs`: add atomic reservation, source-identity lookup, bounded pair loading, canonical alias resolution, and compare-and-set reconciliation contracts with explicit result records.
- `MusicShare.Persistence/Repositories/ShareRequestRepository.cs`: implement named-index duplicate-key recovery, deterministic canonical lookups, alias compare-and-set writes, and alias exclusion in total/recent/weekly pipelines.
- `MusicShare.Persistence/Repositories/ISongServiceLinkRepository.cs` and `MusicShare.Persistence/Repositories/SongServiceLinkRepository.cs`: add a bounded multi-song identity read for reconciliation and exclude alias-only song IDs from public link-count aggregation.
- `MusicShare.Api/Services/ShareIdentityIndexInitializer.cs`: create the named unique sparse `SourceIdentityKey` index during blocking startup and fail startup when the invariant cannot be established.
- `MusicShare.Api/Program.cs`: register the identity index initializer, maintenance settings/security, and reconciliation command dependencies without exposing general API ingress.
- `MusicShare.Services/Services/ShareRequestService.cs`: preserve completed-link reuse, resolve aliases, use atomic source reservation, and publish only for the winning insert.
- `MusicShare.Services/Services/IShareRequestService.cs`: document the canonical-share return behavior without changing the public submit method shape.
- `MusicShare.Services/Services/IDuplicateShareReconciliationService.cs` and `MusicShare.Services/Services/DuplicateShareReconciliationService.cs`: validate exact provider identity evidence, choose a deterministic canonical share, produce side-effect-free dry runs/fingerprints, and apply idempotent aliases through repository abstractions.
- `MusicShare.Services/DependencyInjection.cs`: register the reconciliation service.
- `MusicShare.Services/Models/ShareResultResponse.cs`: keep the existing response shape while documenting that `ShareId` is canonical when an alias was requested.
- `MusicShare.Api/Commands/ReconcileDuplicateShares.cs`: implement the static nested MediatR request/handler/response pattern; orchestrate dry-run/apply, revalidate the two bounded share paths, and publish one metrics refresh after a changed apply.
- `MusicShare.Api/Controllers/MaintenanceController.cs`: expose only the fixed duplicate-pair reconciliation action behind the dedicated maintenance authorization policy.
- `MusicShare.Api/Security/MaintenanceSettings.cs` and `MusicShare.Api/Security/MaintenanceApiKeyAttribute.cs`: bind the dedicated secret, fail closed when absent, and compare the fixed header in constant time.
- `MusicShare.AppHost/AppHost.cs`: declare the maintenance secret and inject it into the private API and public frontend server environments.
- `.github/workflows/ci.yml`: pass `AZURE_MAINTENANCE_SECRET` through both production `azd provision` and `azd deploy` blocks without logging it.
- `.github/workflows/reconcile-duplicate-shares.yml`: add the manual dry-run/apply workflow with strict lowercase ID validation, production environment gate, fingerprint/confirmation requirements, fixed endpoint, bounded output, and no database credentials.
- `MusicShare.Frontend/src/app/api/maintenance/duplicate-shares/route.ts`: authenticate, validate the exact request, forward to the fixed internal API route, bound failures/timeouts, and minimize the response.
- `MusicShare.Frontend/src/lib/api.ts`: represent canonical share IDs in the existing result contract.
- `MusicShare.Frontend/src/app/share/[shareId]/page.tsx`: redirect aliases permanently before rendering/polling and emit canonical metadata URLs.
- `docs/09-duplicate-share-reconciliation.md`: document evidence requirements, dry-run/apply procedure, canonical selection, idempotency, audit fields, rollback/escalation, and the prohibition on direct MongoDB editing.
- `MusicShare.Tests/Integration/ShareRequestDeduplicationMongoTests.cs`: use real MongoDB to create the unique sparse index, race concurrent reservations, prove one winner/request, cover historical rows without keys, and test compare-and-set/idempotent reconciliation.
- `MusicShare.Tests/Unit/Services/ShareRequestServiceTests.cs`: cover completed-link reuse, pending/processing reservation reuse, only-winner publication, alias resolution, missing provider IDs, and propagation of unrelated persistence failures.
- `MusicShare.Tests/Unit/Services/DuplicateShareReconciliationServiceTests.cs`: cover exact shared identities, metadata-only false positives, deterministic/explicit canonical choice, dry-run purity/fingerprint, stale/conflicting state, cycles/chains, idempotent apply, and bounded results.
- `MusicShare.Tests/Unit/Persistence/ShareRequestRepositoryMetricsTests.cs` and `MusicShare.Tests/Integration/PublicMetricsMongoRepositoryTests.cs`: prove aliases are excluded from totals, recent songs, weekly counts, and link counts while canonical entries remain.
- `MusicShare.Tests/Unit/Api/Commands/ReconcileDuplicateSharesHandlerTests.cs`, `MusicShare.Tests/Unit/Api/Controllers/MaintenanceControllerTests.cs`, and `MusicShare.Tests/Unit/Api/Security/MaintenanceApiKeyAttributeTests.cs`: cover orchestration, no side effects during dry-run, one refresh/revalidation sequence after apply, fixed-time auth behavior, absent/invalid secrets, input validation, and minimal responses.
- `MusicShare.Frontend/src/app/api/maintenance/duplicate-shares/route.test.ts`: cover missing/invalid configuration and keys, malformed/mixed/extra inputs, invalid IDs/fingerprints/modes, timeout/backend failures, fixed target forwarding, and bounded response behavior.
- `MusicShare.Frontend/src/app/share/[shareId]/page.test.tsx`: cover canonical metadata and permanent alias redirect while preserving canonical rendering/polling behavior.

## Validation plan

- Run focused unit tests for share creation, reconciliation service/command/controller/security, repository metrics, the maintenance proxy, and share-page redirects.
- Against `MUSICSHARE_TEST_MONGODB=mongodb://127.0.0.1:27017`, launch many concurrent reservations for one Spotify identity and assert one `ShareRequest`, one inserted winner, one share ID returned to every caller, and one published message at the service boundary.
- Seed historical duplicate rows without `SourceIdentityKey` before index creation and prove the sparse index deploys. Dry-run and apply a validated pair, rerun apply, and verify one direct alias, no deletions, stable canonical selection, and stale/conflicting operations rejected.
- Seed same-title/different-provider-ID songs and prove reconciliation rejects them. Seed partial/missing/malformed links and alias chains/cycles and prove fail-closed behavior.
- Run `dotnet build MusicShare.slnx --configuration Release` and `dotnet test MusicShare.Tests/MusicShare.Tests.csproj --configuration Release`.
- From `MusicShare.Frontend`, run focused Vitest files followed by `npm test`, `npm run lint`, and `npm run build`.
- Validate the manual workflow with a non-production/mock target or route tests; do not run an apply against production during implementation review or CI.
- After a future merge/deployment, separately verify deployment success, run the documented production dry-run, obtain explicit operator approval for apply, then verify redirect status/location, canonical metadata, both platform links, refreshed metrics, logs, and the GitHub Actions audit record.
- Run `git diff --check` and confirm no general re-indexing, list-all, delete, Mongo credential, or unrelated provider changes entered the branch.

## Risks and edge cases

- A read-before-insert pending lookup is insufficient under true concurrency. The unique sparse index and duplicate-key recovery are the correctness boundary; startup must not accept writes before the index exists.
- Mongo sparse uniqueness includes an explicitly stored `null` value. `SourceIdentityKey` must be omitted from BSON when null so historical/unidentifiable requests do not collide.
- Existing duplicate rows cannot be backfilled blindly because they would conflict. Only the validated canonical record receives the identity key; aliases remain keyless and point to the canonical share.
- Insert succeeds before message publication in the current architecture. A process failure in that interval is an existing delivery-risk boundary. Do not weaken dedupe to compensate; if implementation demonstrates a practical stranded-request case, route transactional outbox/recovery through a separately approved design or document the bounded recovery within this issue before coding.
- `GetByServiceAndSongIdAsync` can find a historical duplicate link. Submission must resolve the associated request's alias so it never returns a non-canonical ID after reconciliation.
- Exact same-provider identity proves the demonstrated race. Same-title metadata does not. Concurrent cross-provider submissions may remain separate until they share at least one exact resolved provider identity; this plan's operator workflow can reconcile them only after that proof exists.
- Alias records retain their duplicate Song and links. Every public aggregation must start from non-alias canonical requests, or historical duplicates will continue to inflate counts. Direct repository callers need regression coverage.
- Redirecting an indefinitely cached alias page requires explicit ISR revalidation. Apply must treat alias/canonical path revalidation and metrics refresh as bounded follow-up outcomes and report failures for safe retry without rolling back the durable alias.
- A permanent redirect is externally cached. Canonical selection therefore must be deterministic, validated, and protected by a stale-state fingerprint; ordinary operations must never remap an existing alias.
- A public server route protected only by a shared secret is security-sensitive. Keep it pair-scoped, constant-time authenticated, rate/bounds conscious, independently validated on both frontend and API, minimally disclosive, and separately rotatable. Never expose the private API or accept arbitrary backend URLs/paths.
- The former re-indexing routes were removed because they were incomplete and overly broad. This implementation must not restore all-ID enumeration, re-index-all, arbitrary song commands, or a generic maintenance proxy.
- GitHub production-environment protection may require human approval. Dry-run should remain the default, and apply must require both the exact fingerprint and explicit confirmation.
- The public ObjectId evidence suggests `7c9ff089371a` is earlier, but authoritative `ShareRequest.CreatedAt` and exact link identities decide. The future dry-run must stop if production state disagrees.

## Definition of done

- [ ] Concurrent submissions of one validated provider track converge on one canonical ShareRequest/share ID across pending, processing, and completed states, and only the insert winner publishes `SongShareSubmitted`.
- [ ] A named unique sparse source-identity index is established before write traffic; historical keyless rows do not block deployment, and index failure fails startup rather than silently restoring the race.
- [ ] Existing completed cross-provider reuse remains intact, and every reuse path resolves aliases to a terminal canonical share.
- [ ] Reconciliation accepts exactly two valid share IDs, requires at least one exact shared provider identity, rejects metadata-only/ambiguous/conflicting records, and chooses a deterministic or explicitly validated canonical share.
- [ ] Dry-run is side-effect free and returns a bounded stale-state fingerprint; apply is compare-and-set, idempotent, directly aliases without deleting data, records reconciliation evidence, and cannot create chains/cycles or remap aliases.
- [ ] Alias API/page requests resolve to and permanently redirect to the canonical share, with canonical metadata, while old URLs remain usable.
- [ ] Alias records contribute zero duplicate entries to total, recent, weekly, and service-link public metrics; the canonical song remains counted once.
- [ ] A dedicated, narrowly scoped, authenticated maintenance route plus manual dry-run/apply GitHub workflow lets the owner reconcile the Chicago pair without MongoDB access or credentials.
- [ ] The operator runbook documents evidence, dry-run, explicit apply approval, idempotent retry, verification, audit, rollback/escalation, and the no-direct-Mongo rule.
- [ ] Real-Mongo concurrency/reconciliation tests, focused/full backend and frontend tests, Release build, frontend lint/build, workflow validation, and `git diff --check` pass.
- [ ] No general re-indexing, list-all, delete, public backend ingress, fuzzy merge, unrelated provider work, or direct production data mutation is included.
