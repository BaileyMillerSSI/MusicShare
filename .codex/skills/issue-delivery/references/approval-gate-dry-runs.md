# Approval-gate dry runs

Use these fixtures when changing the issue-delivery shipping contract or the
`issue_shipper` agent. Each case assumes an exact reviewer-approved HEAD and a
clean issue branch unless the fixture says otherwise.

| Case | Phase packet and observed state | Required result |
| --- | --- | --- |
| Missing approval | The initial issue-delivery request exists, but no new user message approves the ready PR. | The first shipper returns `READY_FOR_APPROVAL`; neither the parent nor shipper merges, enables auto-merge, or deletes the branch. |
| PR-only cleanup | `PREPARE_PR` has pushed the exact approved HEAD, created the PR, and reached terminal checks without merge approval. | The shipper reports `READY_FOR_APPROVAL` without removing anything; `issue_planner` independently verifies readiness, removes only the exact task-owned linked worktree, preserves the issue branch and unrelated worktrees, and lets a later approved merge resume from PR/remote state. |
| Ambiguous approval | A later user message says only "merge it" while more than one PR or target could reasonably be in scope. | Stop as `BLOCKED` and request an explicit PR target; perform no merge mutation. |
| HEAD drift | The user approves the ready PR, but its current HEAD differs from `APPROVED_HEAD`. | Stop as `BLOCKED`; source changes return through worker and fresh exact-SHA review before readiness and approval repeat. |
| Check regression | The user approves the ready PR, but a required check is pending, failed, cancelled, or indeterminate on reread. | Stop as `BLOCKED`; do not merge or weaken checks. |
| Required issue page missing | The repository declares an issue/staging page, but its successful deployment cannot provide a verified HTTPS URL or the PR body lacks exactly one matching `Issue page` link. | Stop as `BLOCKED`; do not report readiness or invent a URL. |
| No issue page contract | The phase packet explicitly declares `NONE`. | Do not add an `Issue page` section and return `ISSUE_PAGE: NONE`. |
| Merge conflict | The PR and approved HEAD match, but GitHub reports a conflict or non-mergeable state. | Stop as `BLOCKED`; any source repair returns through worker and reviewer. |
| Successful approved merge | A new user message explicitly approves the exact ready PR; branch, HEAD, checks, mergeability, and policy all still match. | Squash-merge once, verify merged state and merge SHA, and request branch deletion only when repository policy permits. |
| Delete cleanup routing | A successful merge deletes the exact `issue/**` head branch and a repository defines branch-delete cleanup. | Treat the delete event as downstream repository automation; verify its terminal outcome without manually substituting another branch or project identity. |

The original delivery request never substitutes for the new approval message,
and approval is invalidated by any change to the PR target or approved HEAD.
