# Duplicate share reconciliation

The service prevents new same-provider races with a MongoDB unique sparse source-identity reservation. Historical duplicate documents are never deleted or edited directly in MongoDB.

## Evidence and dry run

Use **Reconcile duplicate shares** from GitHub Actions with two lowercase 12-character share IDs and `dry-run`. Reconciliation requires at least one exact `(serviceType, serviceSongId)` identity shared by both completed shares. Matching titles, artists, albums, artwork, or durations are not evidence.

The dry run returns a bounded fingerprint, canonical share, alias share, and shared identities. Without an explicit canonical selection, an already pinned canonical is retained; otherwise the earliest `ShareRequest.CreatedAt` wins, then the lexical share ID. Apply first durably pins the selected canonical under its exact reconciliation claim, then writes the direct alias. A pinned record can receive future direct aliases but can never become an alias itself, including after a partial claim takeover or crash. Keep the workflow run URL and operation ID as the audit record.

## Apply and verification

Review the exact provider evidence and dry-run output. Run the workflow again with `apply`, the fingerprint, and `APPLY` confirmation. Production-environment approval remains a separate gate. Apply is compare-and-set and idempotent: a retry of the same completed operation reports `changed: false`.

Apply directly aliases the duplicate, revalidates both bounded share paths, and queues one public-metrics refresh. Verify the alias permanently redirects to the canonical URL, the canonical page preserves links, and metrics count the canonical song once.

## Escalation and rollback

Do not use Mongo shell access, database credentials, list-all operations, arbitrary queries, or deletion to reconcile shares. A stale fingerprint, a conflicting alias, missing completed share, alias chain/cycle, or missing exact identity must fail closed. Escalate with the dry-run output and audit run; permanent redirects are externally cached, so a completed alias is not remapped automatically.
