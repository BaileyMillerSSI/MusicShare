# Planner delivery workflow

Only `issue_planner` reads this full workflow. It owns preflight, investigation, issue definition, the committed plan, worker/reviewer coordination, shipping, and final metadata verification. The top-level chat relays requests, status, and user decisions only.

Read the context and reuse contract in ../SKILL.md. Retain a compact phase ledger: worktree, issue, plan, base/branch, role agent IDs, HEAD, review verdict, review/fix count, auth retry count, PR, readiness and approval scope. Persist a small checkpoint outside tracked source when needed for session recovery; pass its path instead of repeating history. Reconcile durable Git/GitHub state before resuming. Never reset retry counters on replacement.

Worker and reviewer are separate agents. The planner dispatches them as siblings and routes their concise results; the arrows describe phase handoffs, not a requirement to nest each role inside the preceding one. Keep only needed roles live. If agent capacity is exhausted, retire an idle completed phase after checkpointing its state and reconstruct that role only when needed; do not bypass named roles or grow global limits automatically.

## MusicShare repository contract

This package is the current delivery contract for `BaileyMillerSSI/MusicShare`:

- Use remote `origin`, fetch `origin/main`, and use `main` as the base/default branch.
- Create a fresh linked worktree from exactly `origin/main` for each issue. Record its absolute path as task-owned and use that same path through delivery.
- The immutable issue branch is exactly `issue/<issue-number>-<short-kebab-case-slug>`. Never rename it or substitute `codex/`, `feature/`, `fix/`, `feat/`, or another prefix.
- Open pull requests against `main` with `Closes #<issue-number>`. The original delivery request is not merge approval; merge requires a new explicit approval naming the exact ready PR.
- No authoritative issue/staging page contract exists in this repository. Packets must declare `ISSUE_PAGE: NONE` and shippers must not invent an issue/staging URL.
- Cleanup is task-owned and exact: after `issue_shipper` returns `READY_FOR_APPROVAL`, `issue_planner` independently verifies the PR readiness, verifies the recorded linked worktree path still belongs to this issue, and removes only that worktree. This cleanup is required for PR-only delivery and is not deferred until merge. Preserve its local/remote branch plus every existing or unrelated worktree; `issue_shipper` only reports readiness and never performs this cleanup. If ownership or identity is uncertain, stop and report the blocker.
- A later `MERGE_APPROVED` turn resumes from the PR and remote branch state after cleanup; the removed worktree is not required. If source repair is needed, create a fresh task-owned worktree and route the change through `issue_worker -> issue_reviewer` before shipping resumes.

## Required state machine

`REQUEST -> PREFLIGHT -> ISSUE -> PLAN -> IMPLEMENT -> REVIEW -> SHIP_PR -> WAIT_FOR_CHECKS -> READY_FOR_APPROVAL -> USER_APPROVES -> MERGE -> VERIFY -> DONE`

If review rejects:

`REVIEW -> IMPLEMENT_FIX -> REVIEW`

If shipping reveals a source-changing problem:

`SHIP_PR | MERGE -> IMPLEMENT_FIX -> REVIEW -> SHIP_PR`

Never skip independent review. Never ship or merge a code-changing HEAD without a fresh `APPROVED` verdict from `issue_reviewer` for that exact SHA.

The original issue-delivery request authorizes work through `READY_FOR_APPROVAL`; it is never merge approval. `USER_APPROVES` requires a new explicit user instruction that unambiguously identifies the ready pull request. If approval is withheld, leave the pull request and issue branch intact.

## Bounded recovery for agent-scoped GitHub authentication failures

If a named child reports GitHub authentication failure:

1. Planner runs read-only checks in the same repo: `gh auth status`, `gh api rate_limit`, and `gh repo view --json viewerPermission`.
2. If planner authentication/access also fails, stop under the normal auth/permission policy. Do not reauthenticate or alter permissions without user approval.
3. If planner succeeds and evidence indicates an agent-instance/keychain mismatch, retire that child and spawn one fresh instance of the same named agent with `fork_turns: "none"`, the current compact reconstruction packet including retry counters, and a concise note that planner auth succeeded.
4. Retry that phase at most once for this failure class. Repeated failure is a blocker.
5. Before retrying `SHIP_PR` or `MERGE`, reconcile read-only remote state for possible partial side effects: branch pushed, PR created, checks run, or merge completed. Give only the identifiers/state needed for idempotent resume.

An infrastructure-only retry does not invalidate an `APPROVED` verdict if HEAD and working tree are unchanged. A failed `REVIEW` phase still requires a fresh reviewer disposition.

## 1. Preflight

Before creating or modifying repository state:

1. Confirm this is a Git repository and identify the root.
2. Read relevant repository guidance such as `AGENTS.md` and repo-local instructions.
3. Run `gh auth status`.
4. Resolve repository and default branch, preferably with `gh repo view --json nameWithOwner,defaultBranchRef`.
5. Run `git status --short`.
6. Never stash, discard, reset, overwrite, or mix unrelated user changes.
7. If unrelated uncommitted changes cannot be safely isolated, stop.
8. Fetch the remote default branch so planning starts current.
9. Confirm `issue_worker`, `issue_reviewer`, and `issue_shipper` are available for nested delegation.

Never force-push, use `--admin`, bypass protection, disable tests/checks, or weaken CI to finish the workflow.

## 2. Create the GitHub issue

Create a concise engineering issue containing:

- Summary
- User/request context
- Scope
- Acceptance criteria
- Explicit non-goals when evident
- Validation expectations

Do not invent product requirements. Resolve repository facts by inspection. Preserve genuine ambiguity as an explicit assumption or blocker.

Use `gh issue create` and capture the real issue number and URL. If the user explicitly supplied an existing issue, read and use that issue's real number instead; never guess or reserve a number.

Derive a short kebab-case slug and define:

- `ISSUE_NUMBER=<number>`
- `ISSUE_SLUG=<slug>`
- `PLAN_PATH=docs/issues/${ISSUE_NUMBER}-${ISSUE_SLUG}.md`
- `BRANCH=issue/${ISSUE_NUMBER}-${ISSUE_SLUG}`

`ISSUE_SLUG` must be a short kebab-case slug. The exact branch form is mandatory: `issue/<issue-number>-<short-kebab-case-slug>`. Do not use `codex/`, `feature/`, `fix/`, or another prefix during issue delivery.

Create an isolated worktree for `BRANCH` from freshly fetched `origin/main` before implementation begins. Record its absolute path and use it for every downstream phase. Treat `BRANCH` as immutable after its first push. Include the exact branch in every initial or reconstruction worker, reviewer, and shipper packet; require each agent to verify it and never rename it. Immediately before the first push, verify the current branch exactly equals `BRANCH`; stop on a mismatch instead of pushing, renaming, or substituting another prefix.

## 3. Plan in issue_planner

The planner plans. Do not delegate architecture or product decisions to `issue_worker`.

Inspect only enough code, tests, configuration, CI, dependencies, and repository guidance to understand the existing architecture and conventions. Prefer targeted searches/reads over broad dumps.

Write `PLAN_PATH`:

```markdown
# Issue #<number>: <title>

- Issue: <url>
- Branch: `<branch>`
- Status: Approved for implementation

## Request
<faithful restatement>

## Repository findings
<only implementation-relevant findings>

## Proposed implementation
<chosen approach and why>

## File-level plan
- `<path>`: <expected change>

## Validation plan
- <tests/build/lint/manual checks>

## Risks and edge cases
- <risk or edge case>

## Definition of done
- [ ] <objective acceptance criterion>
```

The plan must be implementation-ready so the worker does not need new architecture/product decisions.

Commit only the plan file on `BRANCH`, e.g.:

`docs: plan implementation for #<issue>`

Add a GitHub issue comment containing only the branch and plan path.

## 4. IMPLEMENT — delegate to `issue_worker`

For initial implementation, spawn `issue_worker` with `fork_turns: "none"` and retain its agent ID.

Bounded packet:

- repository/worktree absolute path
- issue number and URL
- default/base branch
- issue branch
- instruction to verify the current branch exactly equals the supplied issue branch and never rename it
- exact `PLAN_PATH`
- instruction that the plan is approved architecture
- only essential repository guidance discovered during planning
- satisfy every Definition of Done item
- add/update tests identified by the plan
- run relevant validation
- no PR creation/merge
- no unrelated refactors
- commit implementation on the issue branch

Semantic contract:

> Implement the approved plan at `PLAN_PATH` on `BRANCH`. Work only within issue scope. Run relevant validation and commit when reviewable. Do not create or merge a PR. Return only the required status envelope.

Required worker envelope:

```text
STATUS: COMPLETE | BLOCKED
HEAD: <sha or NONE>
VALIDATION: PASS | FAIL | NOT_RUN
CONCERNS: <one concise line or NONE>
```

Do not request files-changed lists, raw validation output, diffs, or prose summaries unless `STATUS: BLOCKED` requires concise evidence.

After completion, planner verification should normally be limited to:

- `git status --short`
- `git rev-parse HEAD`
- concise commit metadata such as `git log -2 --oneline`

Verify a new coherent implementation HEAD exists and the working tree is in the expected state. Do not re-read implementation files merely to duplicate review.

## 5. REVIEW — delegate to `issue_reviewer`

For the first review, spawn `issue_reviewer` with `fork_turns: "none"`. Retain its agent ID for subsequent review turns.

Bounded packet:

- repository/worktree absolute path
- issue number and URL
- `PLAN_PATH`
- base/default branch
- issue branch
- instruction to verify the current branch exactly equals the supplied issue branch and never rename it
- exact implementation HEAD SHA
- worker validation status only (`PASS`, `FAIL`, or `NOT_RUN`), not raw logs

Require reviewer to:

1. Read the issue and `PLAN_PATH` from their sources.
2. Review the complete branch diff against the remote default branch.
3. Inspect correctness, completeness, architecture fit, regressions, error handling, edge cases, security/performance where relevant, and test quality.
4. Independently inspect/run targeted validation as useful; do not rely on relayed worker logs.
5. Verify every Definition of Done item objectively.
6. Return exactly one terminal disposition tied to the exact HEAD SHA.
7. Never modify source/application code or merge.

Required reviewer envelope:

```text
VERDICT: APPROVED | CHANGES_REQUIRED
HEAD: <exact reviewed sha>
BLOCKERS: NONE
```

For `CHANGES_REQUIRED`, replace `BLOCKERS: NONE` with only concise actionable blocking findings, preferably `path:line — finding` where useful. Do not return a review essay.

### Review/fix loop

If `CHANGES_REQUIRED`:

1. Resume the same worker with `followup_task` (or the equivalent resume tool). Send only phase `IMPLEMENT_FIX`, current HEAD, concise blocking findings, and changed constraints or reference paths.
2. Require fixes for blocking findings, relevant validation, and a fix commit; retain the same worker envelope.
3. Verify the new HEAD and working-tree status using concise metadata.
4. Resume the same independent reviewer with phase `REVIEW`, previous reviewed SHA, new HEAD, validation status, and changed reference paths. It inspects the new diff and its effect on the complete branch and issues a new exact-SHA verdict. A fresh verdict does not require a fresh agent.
5. Never give the reviewer the worker's reasoning narrative. If an agent is unavailable or has lost its context, spawn the same named role with `fork_turns: "none"` and the compact reconstruction packet described in SKILL.md.

Allow at most 10 review/fix cycles. Then stop and report unresolved blocking findings.

Never proceed to shipping without `APPROVED` for the exact current HEAD.

## 6. SHIP_PR — delegate approval readiness to `issue_shipper`

Only after exact-SHA approval, spawn `issue_shipper` with `fork_turns: "none"` for its first turn. If shipping resumes after a repair, reuse its agent ID and send phase PREPARE_PR, the newly approved SHA/verdict, validation status, and changed references only.

Bounded packet:

- repository/worktree absolute path
- issue number and URL
- default/base branch
- issue branch
- instruction to verify the current branch exactly equals the supplied issue branch and never rename it
- `PLAN_PATH`
- exact reviewer-approved HEAD SHA
- reviewer verdict
- worker validation status only
- issue/staging page expectation plus its repository-defined authoritative discovery method, or explicit `NONE` (MusicShare: `NONE`)
- explicit phase: `PREPARE_PR`

Require shipper to own Git/GitHub mechanics only for this first phase:

1. Confirm current HEAD equals approved SHA.
2. Confirm working tree is clean.
3. Push without force-push.
4. Open a PR to the default branch.
5. Include `Closes #<issue>`.
6. Include concise implementation summary derived from Git/repo state, `PLAN_PATH`, validation status, and approved SHA.
7. Capture PR number and URL.
8. Watch required checks to terminal, preferably `gh pr checks <pr> --watch` when supported.
9. If a required check fails/cancels/is indeterminate, stop and return evidence; do not merge.
10. If no checks are configured, report that accurately.
11. When the packet declares an issue/staging page, resolve its URL only from the repository-defined authoritative source after the related deployment check passes. Require HTTPS, preserve the rest of the PR body, and idempotently create or replace an `## Issue page` section containing the exact link. If the page is expected but missing, unverifiable, or absent from the resulting PR body, return `BLOCKED`. When the packet declares `NONE`, do not invent a URL or section.
12. Re-read PR HEAD SHA and confirm it still equals the approved SHA.
13. Respect branch protection, required reviews, merge queues, and repository rules.
14. Return `READY_FOR_APPROVAL`; do not remove the worktree, merge, enable auto-merge, or delete the branch. Worktree cleanup belongs to the planner after it independently verifies this readiness.

`issue_shipper` must never modify source/application code. Any source-changing repair routes through `issue_worker -> issue_reviewer` before shipping resumes.

Required shipper envelope:

```text
STATUS: READY_FOR_APPROVAL | BLOCKED
PR: <number + url or NONE>
CHECKS: PASS | FAIL | NONE | INDETERMINATE
APPROVED_HEAD: <sha or NONE>
ISSUE_PAGE: <https URL or NONE>
MERGE_SHA: <sha or NONE>
BLOCKER: <one concise line or NONE>
```

Do not return command transcripts, PR body text, raw check logs, or narrative summaries unless needed to explain a blocker.

When the shipper returns `READY_FOR_APPROVAL`, `issue_planner` must independently confirm the PR number/URL, green or accurately absent checks, exact reviewer-approved HEAD, and any declared issue-page URL in the PR description. It must then verify the recorded worktree path and branch identity, remove only that exact task-owned worktree, and confirm that the issue branch and every existing or unrelated worktree remain intact. Only after that cleanup may it return the compact readiness envelope to the top level so the original chat can verify and report it to the user. Do not call merge tools, enable auto-merge, delete the branch, or keep the turn open to infer approval.

## 7. USER_APPROVES and MERGE — explicit second shipper turn

Only a new explicit user instruction approving the exact ready PR authorizes merge. General requests such as the original issue-delivery invocation, "finish the issue," or approval that ambiguously could refer to another PR are insufficient.

After unambiguous approval relayed verbatim by the top level, resume the same `issue_shipper` for a separate merge turn. The original task-owned worktree may already have been removed after PR readiness; merge validation uses the PR and remote branch state, and the missing worktree is not a blocker. If source repair is needed, stop and route it through `issue_worker -> issue_reviewer` in a fresh task-owned worktree. Send phase `MERGE_APPROVED`, the exact PR and approved HEAD, the new user approval, and changed state only. If that agent is unavailable, spawn the same role with `fork_turns: "none"` and this reconstruction packet:

- repository/workspace context for GitHub/Git checks; the original task-owned worktree may already be absent after `PREPARE_PR` cleanup
- issue number and URL
- default/base branch
- exact immutable issue branch
- `PLAN_PATH`
- exact PR number and URL reported ready
- exact reviewer-approved HEAD SHA
- reviewer verdict and worker validation status only
- issue/staging page expectation and the exact verified URL reported ready, or explicit `NONE` (MusicShare: `NONE`)
- the new user approval instruction identifying that PR
- instruction to verify the branch and never rename it
- explicit phase: `MERGE_APPROVED`

Require the shipper to:

1. Read the PR again and require it to remain open and sourced from the exact issue branch.
2. Confirm the PR HEAD still equals the reviewer-approved SHA.
3. Re-read required checks and require them to remain terminal and passing, or accurately confirm none are configured.
4. Confirm GitHub reports the PR mergeable under current repository policy.
5. When an issue page was required at readiness, confirm the PR description still contains the exact verified HTTPS URL in its `Issue page` section.
6. Stop without merging on HEAD drift, new commits, pending/failed/indeterminate checks, missing issue-page evidence, merge conflict, ambiguous approval, or policy requirements. Source-changing repairs return through `issue_worker -> issue_reviewer -> SHIP_PR` and need a new user approval after readiness is re-established.
7. Squash merge without force or `--admin`, request remote branch deletion only when policy permits, and verify the merged state and merge SHA.

Required merge-phase shipper envelope:

```text
STATUS: MERGED | BLOCKED
PR: <number + url or NONE>
CHECKS: PASS | FAIL | NONE | INDETERMINATE
APPROVED_HEAD: <sha or NONE>
ISSUE_PAGE: <https URL or NONE>
MERGE_SHA: <sha or NONE>
BLOCKER: <one concise line or NONE>
```

Approval is single-use and applies only to the exact PR and approved HEAD in the packet.

## 8. VERIFY — planner final verification

After shipping reports success, independently verify using concise GitHub/Git metadata:

- PR is merged
- merged PR references/closes the issue
- issue is closed; close explicitly only if the merged PR failed to do so
- PR references `PLAN_PATH`
- merged PR history corresponds to the reviewer-approved SHA
- no unexpected local changes remain
- PR-only delivery cleanup was completed after readiness; existing worktrees and local/remote branches remain intact

Avoid re-reading implementation source during normal final verification.

Return a compact delivery summary:

- issue number + URL
- plan path
- implementation/fix commit SHA(s)
- reviewer verdict + exact reviewed SHA
- PR number + URL
- CI/check outcome
- issue-page URL when the repository declared one
- squash merge outcome + merge SHA when available

Do not delete the plan file or rewrite history after merge unless explicitly requested.

## Failure policy

Continue automatically through normal phases, but stop rather than guess when any of these occur:

- GitHub auth/permission failure reproduced by planner or still failing after the single bounded fresh-agent retry
- unrelated dirty working tree that cannot be safely isolated
- missing named custom agent
- delegation cannot honor `fork_turns: "none"`
- unresolved product/architecture ambiguity that materially changes behavior
- implementation cannot satisfy the approved plan
- 10 failed review/fix cycles
- failing/cancelled/indeterminate required checks
- required human approval
- merge conflict or branch drift requiring source changes
- repository protection prevents squash merge

Never silently replace a named agent, weaken validation/protection, force-push protected history, or use `--admin` merely to complete the pipeline.

When maintaining this approval contract, run the safety cases in [approval-gate-dry-runs.md](approval-gate-dry-runs.md).
