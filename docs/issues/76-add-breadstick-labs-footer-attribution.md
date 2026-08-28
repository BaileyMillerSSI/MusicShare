# Issue #76: Add Breadstick Labs footer attribution

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/76
- Branch: `issue/76-add-breadstick-labs-footer-attribution`
- Status: Approved for implementation

## Request

Add the exact copy "Proudly baked by Breadstick Labs" in a new footer or another visually low-importance area, with "Breadstick Labs" linking to exactly `https://breadsticklabs.com/`.

## Repository findings

- The public UI is the Next.js App Router frontend under `MusicShare.Frontend/src`; the backend and Aspire configuration are outside this presentation-only change.
- The homepage (`src/app/page.tsx`) and share-result page (`src/app/share/[shareId]/page.tsx`) each own a full-height purple-to-pink gradient and a centered white primary card.
- There is no current footer or shared attribution component. The two public display pages are the relevant rendering surfaces; the repository has no email template or email-sending surface to update.
- Frontend components use React function components and Tailwind utility classes, with colocated Vitest and Testing Library tests.
- CI requires frontend lint, test, and production build jobs for pull requests to `main`.

## Proposed implementation

Create a small semantic `BreadstickFooter` component and render it beneath the primary card on both public display pages. Convert each page's existing outer flex container to a column layout so the footer participates in normal document flow rather than using fixed or overlay positioning. Style the footer with centered, small, partially transparent white text and a subtle link hover/focus treatment. This keeps the attribution readable on the gradient while preserving the existing card as the dominant element.

The link must use the exact requested trailing-slash URL without analytics parameters. Do not add a new-tab behavior unless the implementation can also provide the corresponding safe relationship attributes; same-tab navigation is acceptable and keeps the contract minimal.

## File-level plan

- `MusicShare.Frontend/src/components/BreadstickFooter.tsx`: add the reusable semantic footer with the exact attribution copy, destination, and subdued responsive styling.
- `MusicShare.Frontend/src/components/BreadstickFooter.test.tsx`: verify the exact visible attribution and exact link destination.
- `MusicShare.Frontend/src/app/page.tsx`: render the shared footer below the homepage card and adjust the page flex direction/spacing without changing the card contents.
- `MusicShare.Frontend/src/app/share/[shareId]/page.tsx`: render the same footer below the share-result card and make the matching non-overlay layout adjustment.

## Validation plan

- Run `npm test` from `MusicShare.Frontend`.
- Run `npm run lint` from `MusicShare.Frontend`.
- Run `npm run build` from `MusicShare.Frontend`.
- Run `git diff --check` from the repository root.
- Inspect the rendered structure or focused tests to confirm the footer remains in normal flow beneath each card and the exact URL includes no query string.

## Risks and edge cases

- A fixed footer could cover content on short mobile viewports; normal flow avoids that risk.
- Moving the attribution into the root layout would place it outside the page-owned gradient because both pages currently own `min-h-screen`; page composition with a shared component preserves the visual background.
- The existing page structure tests assert selected Tailwind classes. Preserve those existing class contracts while adding column layout and spacing.
- External-link tracking parameters or broadened email/backend work would exceed the explicit request.

## Definition of done

- [ ] A reusable `BreadstickFooter` renders the exact visible copy "Proudly baked by Breadstick Labs".
- [ ] The `Breadstick Labs` anchor points exactly to `https://breadsticklabs.com/` with no query parameters.
- [ ] The shared footer appears beneath the primary card on both the homepage and share-result page.
- [ ] The footer is visually secondary, responsive, keyboard-visible, and remains in normal document flow without obscuring the primary UI.
- [ ] Focused component coverage verifies the exact copy and destination.
- [ ] Frontend tests, lint, production build, and `git diff --check` pass.
