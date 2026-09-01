# Issue #92: Display metric week starts in the viewer's local time

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/92
- Branch: `issue/92-local-metrics-time`
- Status: Approved for implementation

## Request

Adjust the public `/metrics` weekly chart so that, once client-side code is active, its week-start timestamps use the viewer's local time. Preserve hydration correctness by ensuring the server output and first browser render are identical.

## Repository findings

- `MusicShare.Frontend/src/app/metrics/page.tsx` is an async server component with `dynamic = 'force-dynamic'`. It reads the persisted snapshot through the private Aspire-internal API and currently renders the weekly chart inline.
- Weekly buckets are authoritative Sunday 00:00 UTC instants created by the backend. Their counts, order, keys, and `+N this week` meaning must remain UTC-bucketed; this request changes presentation only.
- The chart currently derives deterministic labels with string slices such as `MM-DD UTC` and an accessible `YYYY-MM-DD UTC: N songs`. Those are safe for SSR but never adapt to the browser time zone.
- Browser locale/time-zone formatting during render would risk different server and initial-client markup. A client component can safely render the existing UTC form from deterministic initial state, then switch to local formatting from `useEffect` after hydration.
- React, Testing Library, Vitest, and happy-dom are already available. No date, chart, or hydration-suppression dependency is needed.
- The route's private fetch, safe fallback, metadata/social image, chart geometry, and responsive Tailwind styling are unrelated and should remain unchanged.

## Proposed implementation

Extract the existing weekly chart into a focused client component that receives the validated weekly buckets. Preserve the exact UTC labels during SSR and the browser's first render. After mount, set a boolean client-ready state in `useEffect`; only then format each valid `weekStart` instant through the browser's `Intl.DateTimeFormat` without supplying a `timeZone`, allowing the browser's locale and local zone to apply.

Keep a compact visible date plus time-zone abbreviation and a fuller accessible label describing the same local instant and song count. Retain the original ISO timestamp in `<time dateTime>`, use the ISO value as the stable React key, and do not alter bar heights or bucket counts. If local formatting unexpectedly fails, retain the deterministic UTC label rather than breaking the chart.

Do not branch on `window`, call `Intl` in a state initializer, use `suppressHydrationWarning`, or derive different initial markup on the server and browser. The only transition to local labels occurs in the post-hydration effect.

## File-level plan

- `MusicShare.Frontend/src/app/metrics/WeeklyCompletedSongsChart.tsx`: add the client component, deterministic UTC fallback formatter, post-mount local formatter, accessible labels, and the existing semantic/responsive bar markup.
- `MusicShare.Frontend/src/app/metrics/WeeklyCompletedSongsChart.test.tsx`: cover deterministic server markup, post-mount local formatting including a zone behind UTC that shifts to the prior date, unchanged counts/heights/dateTime values, formatter failure fallback, and hydration without mismatch diagnostics.
- `MusicShare.Frontend/src/app/metrics/page.tsx`: replace the inline weekly-chart implementation with the new component while preserving server data fetching, summary cards, fallback copy, metadata, and route configuration.
- `MusicShare.Frontend/src/app/metrics/page.test.tsx`: update page-level expectations for the extracted chart boundary and retain request-time rendering, summary, empty/error, metadata, and integration coverage.

## Validation plan

- From `MusicShare.Frontend`, run focused Vitest coverage for `src/app/metrics/WeeklyCompletedSongsChart.test.tsx` and `src/app/metrics/page.test.tsx`.
- Run the full frontend `npm test` suite.
- Run `npm run lint`.
- Run `npm run build` and confirm the route table still reports `/metrics` as request-time/dynamic.
- Run `git diff --check` and inspect the branch diff for issue-only changes.

## Risks and edge cases

- A Sunday 00:00 UTC bucket may be Saturday evening in a zone west of UTC. The local label should reflect that actual instant; the bucket's count and identity must not be moved or recalculated.
- Locale output varies by browser. Tests should control/mock the formatter or time zone where exact text matters and verify behavior rather than assume the host locale.
- Effects run immediately in normal Testing Library renders, so explicit server-render/hydrate coverage is needed to prove the pre-effect markup is identical and that no hydration warning is emitted.
- `Intl.DateTimeFormat` can throw for unusual runtime conditions. Falling back to the existing UTC representation keeps the already-validated chart usable.
- The metrics snapshot validator already rejects invalid dates. The client component should still fail safely at its display boundary without widening the API contract.

## Definition of done

- [ ] `/metrics` server HTML and the first client render show the same deterministic UTC weekly labels.
- [ ] After hydration, weekly visible and assistive labels use the viewer's browser locale/time zone.
- [ ] A viewer behind UTC sees the correct prior local calendar date for a `00:00Z` bucket while counts, order, keys, and bar heights remain unchanged.
- [ ] Hydration-focused coverage observes no mismatch/error diagnostics, without using hydration-warning suppression.
- [ ] Formatter failure falls back to the UTC labels without breaking the chart.
- [ ] The existing UTC aggregation/scheduling, private snapshot fetch, no-polling boundary, metadata/share image, safe empty state, and `/metrics` dynamic route behavior remain unchanged.
- [ ] Focused and full frontend tests, lint, production build, and `git diff --check` pass.
