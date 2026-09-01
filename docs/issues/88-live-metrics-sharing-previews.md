# Issue #88: Add live stats to metrics sharing previews

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/88
- Branch: `issue/88-live-metrics-sharing-previews`
- Status: Approved for implementation

## Request

Give the new public metrics page proper share-oriented SEO, with a rich preview that reflects the current public metrics rather than generic static copy.

## Repository findings

- `MusicShare.Frontend/src/app/metrics/page.tsx` is a server component and is the only public metrics surface. It reads `GET /api/metrics` over the Aspire-injected private API service reference, validates the response, and renders a safe zero/empty fallback. The frontend remains the only public service.
- The persisted internal snapshot already contains `totalCompletedSongs`, Spotify and YouTube Music link counts, an optional `generatedAt`, recent songs, and an optional current eight-week series. The current week's count is the last weekly bucket.
- Production inspection on 2026-08-31 showed current page values of 289 completed songs, 289 Spotify links, 268 YouTube Music links, and +6 this week, but the document head contained only a title and generic description. It had no canonical, Open Graph, Twitter card, or share-image metadata. These observed values are evidence only and must never be hardcoded.
- The page currently duplicates its metrics fallback, validation, and private fetch logic locally. Metadata and image generation need the same trusted data boundary and must distinguish a legitimate zero-valued snapshot from an unavailable/malformed fallback so sharing cannot label failure zeroes as current production facts.
- `MusicShare.Frontend/src/app/share/[shareId]/page.tsx` demonstrates the repository's existing `generateMetadata` convention, but its song artwork is external and does not provide a route-specific branded metrics preview.
- Next.js 16.3 supports dynamic metadata from server components and server-generated Open Graph images through `ImageResponse`. Generated images support flexbox and a bounded CSS subset, so the card must avoid grid and browser-only styling.
- Social platforms cache preview images aggressively. A single permanent image URL could keep old counts even when page metadata refreshes, so the metadata must add a snapshot-derived version to the image URL. The image route itself must read the server-side snapshot and remain outside `proxy.ts`; that matcher exposes only `/api/share/:path*`.
- The canonical production route is `https://music.baileymiller.dev/metrics`. No existing general public-origin configuration is needed for this route-specific change.
- The existing issue #86 branch changes `/metrics` from static revalidation to request-time rendering, but it is not part of `origin/main` or this issue. This implementation must not absorb or rewrite that separate issue; its shared helper/metadata changes should remain compatible with either route mode.

## Proposed implementation

Extract the metrics API resolution, response validation, service list, and safe empty value into a server-only module. Have its fetch operation return both the normalized metrics and an availability flag. A valid zero snapshot is available; network, HTTP, JSON, or schema failures produce the existing empty metrics value with `available: false`. Add pure summary helpers that derive completed, Spotify, YouTube Music, and current-week counts plus a deterministic preview version. Prefer `generatedAt` when present and valid, with the validated counts as a stable compatibility fallback.

Replace the metrics page's static metadata export with `generateMetadata`. For a valid snapshot, keep a concise stable page title and generate a factual description containing the four current values. Emit the absolute canonical, `openGraph` (`website`, URL, site name, title, description, 1200x630 PNG and alt text), and `twitter` (`summary_large_image`, matching title/description/image) fields. Point both card types at an absolute frontend image route with the snapshot-derived version query. When metrics are unavailable, emit generic MusicShare metrics copy and a generic image version with no numeric claims.

Add a public frontend-only `GET /metrics/share-image` route backed by `ImageResponse` and a colocated presentation component/helper. It will produce a 1200x630 PNG using the existing purple-to-pink MusicShare visual language, a white content panel, and readable completed/Spotify/YouTube/current-week summaries. The route ignores user-supplied values and fetches the authoritative private snapshot itself; the query version is only a cache key. It is force-dynamic at the frontend boundary, returns explicit image content/cache headers, and renders a branded nonnumeric fallback when the snapshot is unavailable. Do not expose the backend metrics endpoint.

Keep the visible `/metrics` page markup and behavior unchanged, except that it consumes the extracted fetch result. Add focused tests around valid/invalid snapshots, factual and fallback metadata, versioned absolute image URLs, social card fields, image dimensions/content type/cache behavior, and preservation of the page's current fallbacks.

## File-level plan

- `MusicShare.Frontend/src/lib/server/publicMetrics.ts`: centralize the private API origin resolution, accepted platform list, empty response, schema validation, availability-aware fetch, derived summary, factual/fallback share copy, and snapshot preview-version construction.
- `MusicShare.Frontend/src/lib/server/publicMetrics.test.ts`: prove private fetch precedence, valid zero vs unavailable distinction, malformed/HTTP/network containment, platform/weekly validation, count derivation, factual/fallback copy, and stable version behavior.
- `MusicShare.Frontend/src/app/metrics/page.tsx`: consume the shared server helper, preserve the current UI, replace static metadata with dynamic canonical/Open Graph/Twitter metadata, and use one versioned absolute image URL for both card types.
- `MusicShare.Frontend/src/app/metrics/page.test.tsx`: retain rendering coverage and add valid-snapshot and unavailable/malformed `generateMetadata` assertions for canonical URL, exact current values, image URL version, card/type/dimensions/alt, and absence of misleading fallback zero claims.
- `MusicShare.Frontend/src/app/metrics/share-image/MetricsShareImage.tsx`: define the flexbox-only branded 1200x630 card presentation for snapshot and generic fallback modes.
- `MusicShare.Frontend/src/app/metrics/share-image/route.ts`: force request-time image generation, read the authoritative private snapshot, create the `ImageResponse`, and set explicit PNG/cache response behavior without trusting query-string metric values.
- `MusicShare.Frontend/src/app/metrics/share-image/route.test.ts`: mock the metrics boundary and image renderer as needed to verify authoritative snapshot use, fallback mode, 1200x630 configuration, content type, and cache headers.

## Validation plan

- From `MusicShare.Frontend`, run focused Vitest coverage for `src/lib/server/publicMetrics.test.ts`, `src/app/metrics/page.test.tsx`, and `src/app/metrics/share-image/route.test.ts`.
- From `MusicShare.Frontend`, run `npm run lint`, full `npm test`, and `npm run build`.
- Inspect Next's production build route output to ensure `/metrics/share-image` is dynamic and `/api/metrics` has not become a public proxy route.
- Run the built frontend against a controlled/mocked metrics snapshot where practical; inspect `/metrics` head output for canonical/Open Graph/Twitter fields, fetch the image URL, and verify HTTP 200, `image/png`, 1200x630 dimensions, and presence of the supplied test counts.
- Run `git diff --check` and confirm the branch contains only issue-scoped frontend/docs changes.

## Risks and edge cases

- A snapshot can legitimately contain all zeroes. Availability must be based on successful validation, not on count magnitude, so a real empty system can share accurate zero stats while an API failure stays generic.
- `weeklyCompletedSongs` and `generatedAt` are optional for compatibility with older snapshots. Missing weekly data maps to zero for a valid snapshot; missing/invalid `generatedAt` uses validated counts for the image-version key.
- Metadata and image requests are separate HTTP requests, so a snapshot may change between them. The image must always read the latest authoritative snapshot and never accept displayed counts from query parameters; the version query is cache-busting only.
- A generic fallback image URL may be cached during an outage. Once a valid snapshot returns, its versioned URL differs, preventing the fallback cache from suppressing the recovered preview.
- `ImageResponse` supports only a subset of CSS and requires every multi-child container to declare `display: flex` or `display: none`. Keep image layout intentionally simple and validate it through the production build/runtime.
- The image endpoint is public by design, but it reveals only the same aggregate counts already visible on `/metrics`; it must not proxy arbitrary API paths or include private configuration/error details.
- Issue #86 may later touch the same page route configuration. This issue must preserve the base branch's route mode and avoid coupling SEO correctness to static versus request-time page rendering.

## Definition of done

- [ ] Sharing `https://music.baileymiller.dev/metrics` produces absolute canonical, Open Graph, and Twitter `summary_large_image` metadata.
- [ ] A valid snapshot drives the metadata description and branded 1200x630 PNG with actual completed-song, Spotify-link, YouTube Music-link, and current-week counts.
- [ ] The social image URL is versioned from the validated snapshot and changes when its generated version or displayed metrics change.
- [ ] API, HTTP, JSON, and schema failures produce honest generic metadata/image content with no numeric fallback claims; a valid all-zero snapshot remains accurately numeric.
- [ ] The image route fetches only the private Aspire metrics endpoint on the server and does not expose or proxy that backend route publicly.
- [ ] The visible metrics page, recent canonical share links, weekly chart, response validation, and safe page fallback remain intact.
- [ ] Focused tests plus full frontend lint, tests, production build, and image/head inspection pass.
