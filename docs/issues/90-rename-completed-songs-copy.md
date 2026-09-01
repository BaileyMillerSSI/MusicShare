# Issue #90: Rename completed songs copy on metrics page

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/90
- Branch: `issue/90-rename-completed-songs-copy`
- Status: Approved for implementation

## Request

On the literal `/metrics` page, and in its SEO metadata where the wording is also present, change “Completed songs” to “Songs”.

## Repository findings

- `MusicShare.Frontend/src/app/metrics/page.tsx` renders the public metrics page. The phrase appears in its introductory sentence, primary metric-card label, weekly chart heading and accessible labels, per-week accessible count, and recent-song empty state.
- `MusicShare.Frontend/src/lib/server/publicMetrics.ts` builds the factual description and image alt text consumed by the page metadata, Open Graph metadata, and Twitter metadata. Available snapshots currently describe the total as “completed songs”.
- `MusicShare.Frontend/src/app/metrics/share-image/MetricsShareImage.tsx` renders the social-preview image and currently labels the total-count card “Completed songs”.
- Focused coverage exists in the colocated metrics page and share-image tests plus `MusicShare.Frontend/src/lib/server/publicMetrics.test.ts`.
- Internal API and frontend types intentionally use names such as `totalCompletedSongs`, `weeklyCompletedSongs`, and `completedSongs`. Those are data-contract and implementation identifiers, not public copy, and do not need renaming.
- The frontend CI gate runs npm install, lint, the full Vitest suite, and a production Next.js build.

## Proposed implementation

Replace only public-facing uses of “Completed songs” or “completed songs” in the `/metrics` experience with context-appropriate “Songs” or “songs”. This includes visible page copy, accessibility text, available-snapshot SEO/social metadata, metadata image alt text, and the generated social-preview image. Preserve the existing counts, calculations, layout, routes, metadata structure, fallback metadata, and internal completion-oriented identifiers. Keep the separate “Completed this week” wording unchanged because the request targets the “Completed songs” label and phrase.

## File-level plan

- `MusicShare.Frontend/src/app/metrics/page.tsx`: update the introductory total, primary card label, weekly chart heading and accessible labels, per-week accessible count, and recent-song empty state to use “Songs”/“songs”; retain internal identifiers and data names.
- `MusicShare.Frontend/src/lib/server/publicMetrics.ts`: update available-snapshot metadata description and image alt copy so the total is described as “songs”.
- `MusicShare.Frontend/src/app/metrics/share-image/MetricsShareImage.tsx`: relabel the total-count preview card as “Songs”.
- `MusicShare.Frontend/src/app/metrics/page.test.tsx`: update visible, accessible, empty-state, and metadata expectations for the new wording, including regression coverage that the old phrase is absent from public output.
- `MusicShare.Frontend/src/lib/server/publicMetrics.test.ts`: update the metadata-copy assertion to expect “songs”.
- `MusicShare.Frontend/src/app/metrics/share-image/MetricsShareImage.test.tsx`: update the social-preview label expectation and layout selector to “Songs”.

## Validation plan

- Run focused Vitest coverage for the metrics page, public metadata helper, and metrics share-image component.
- Run `npm test` from `MusicShare.Frontend`.
- Run `npm run lint` from `MusicShare.Frontend`.
- Run `npm run build` from `MusicShare.Frontend`.
- Run `git diff --check`.

## Risks and edge cases

- SEO copy is assembled in a shared helper, so tests must verify the Next metadata output and the helper output remain synchronized.
- Accessibility strings are user-facing even when not visibly rendered; leaving the old phrase there would produce inconsistent wording for assistive technology.
- Internal identifiers must remain unchanged to avoid turning a copy-only request into an API or data-contract migration.
- Unavailable metrics use generic metadata without a numeric song claim; that fallback should remain unchanged.

## Definition of done

- [ ] The `/metrics` primary total is labeled “Songs”, and its other public page copy no longer uses the phrase “completed songs”.
- [ ] Available-snapshot canonical, Open Graph, and Twitter metadata describe the total as “songs”, with matching image alt copy.
- [ ] The generated metrics social-preview image labels the total “Songs”.
- [ ] Counts, calculations, links, page behavior, fallback metadata, and internal data/API identifiers are unchanged.
- [ ] Focused and standard frontend validation passes.
