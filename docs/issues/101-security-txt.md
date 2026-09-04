# Issue #101: Publish and verify security.txt

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/101
- Branch: `issue/101-security-txt`
- Status: Approved for implementation

## Request

Publish a repository-owned `/.well-known/security.txt` from the public MusicShare frontend. The record must direct researchers to the site's existing public contact page, carry a renewable expiry, identify its canonical well-known URL, prefer English, and be protected by a production-build assertion so it cannot silently drift or disappear.

## Repository findings

- `MusicShare.Frontend` is the only public-facing service and its `public` directory is copied into the standalone production image by `MusicShare.Frontend/Dockerfile`.
- The canonical MusicShare production origin is `https://music.baileymiller.dev`.
- MusicShare's footer links to Breadstick Labs, whose existing public contact section is `https://breadsticklabs.com/#contact`; no MusicShare-local contact page or security contact is present.
- The frontend production command is `npm run build`, currently backed by `next build --webpack`, and the CI frontend job already runs lint, tests, and that build.
- Frontend tests use Vitest and include colocated or otherwise discoverable `*.test.ts` files.

## Proposed implementation

Add the security.txt record as a static public asset at the well-known path so Next.js and the existing standalone Docker image serve it without introducing a new backend or dynamic route. The record will contain exactly these fields in this order, ending with a newline:

1. `Contact: https://breadsticklabs.com/#contact`
2. `Expires: 2027-09-03T00:00:00Z`
3. `Canonical: https://music.baileymiller.dev/.well-known/security.txt`
4. `Preferred-Languages: en`

Add an ESM validation module invoked after `next build --webpack`. It will read the production public asset, require exactly the expected non-empty fields and values (with a syntactically valid UTC `Expires` timestamp), and require at least 30 days of remaining validity. This intentionally turns an approaching expiry, missing file, duplicate/unknown field, changed contact/canonical/language, or malformed record into a failed production build while leaving the annual expiry explicit and reviewable in the repository. Export the validation logic so Vitest can cover success and failure cases without shelling out.

## File-level plan

- `MusicShare.Frontend/public/.well-known/security.txt`: add the four-field public security contact record with the explicit renewable expiry.
- `MusicShare.Frontend/scripts/assert-security-txt.mjs`: parse and validate the record contract, expose testable validation functions, and provide the production-build CLI entry point.
- `MusicShare.Frontend/scripts/assert-security-txt.test.ts`: cover a valid record and failures for disappearance/content drift, invalid or insufficient expiry, and unexpected/duplicate fields.
- `MusicShare.Frontend/package.json`: append the assertion to the existing production build command so local builds and the existing CI build gate enforce it.

## Validation plan

- Run `npm run lint` from `MusicShare.Frontend`.
- Run `npm test` from `MusicShare.Frontend`, including focused validator tests.
- Run `npm run build` from `MusicShare.Frontend` and confirm the security.txt assertion succeeds after the Next.js production build.
- Start the resulting standalone server or equivalent production server locally and request `/.well-known/security.txt`; confirm HTTP 200, plain-text content, and the exact four-field record.
- Run `git diff --check` and confirm no unexpected working-tree changes remain.

## Risks and edge cases

- A static asset is only production-visible while the standalone image continues copying `public`; the build assertion protects the record itself, and the existing Dockerfile is the production packaging contract.
- The explicit expiry must be renewed in source. The 30-day build-time safety window prevents a routine production build from shipping an almost-expired or expired record, but it does not replace operational scheduling when no builds occur.
- Do not introduce a speculative mailbox, policy URL, encryption key, acknowledgments page, or other unsupported security.txt field.

## Definition of done

- [ ] `https://music.baileymiller.dev/.well-known/security.txt` is represented by a repository-owned public asset at the matching path.
- [ ] The record contains the exact Contact, Canonical, and Preferred-Languages values above plus the valid `2027-09-03T00:00:00Z` expiry.
- [ ] The existing production frontend build fails when the record is missing, structurally drifts, or has less than 30 days remaining before expiry.
- [ ] Automated tests cover the validator's passing contract and representative failure cases.
- [ ] Frontend lint, tests, production build, and a local production-path response check pass without unrelated changes.
