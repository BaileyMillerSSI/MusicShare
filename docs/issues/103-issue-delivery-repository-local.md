# Issue #103: Make issue-delivery repository-local

- Issue: https://github.com/BaileyMillerSSI/MusicShare/issues/103
- Branch: `issue/103-issue-delivery-repository-local`
- Status: Approved for implementation

## Request

Check in the current issue-delivery workflow and its required custom agents so MusicShare can use the workflow from the repository, with fresh worktrees from `origin/main`, PRs targeting `main`, exact-SHA independent review, explicit merge approval, and task-owned worktree cleanup.

## Repository findings

- MusicShare already uses `.codex/skills` and `.codex/skills/<name>/agents/openai.yaml` for repository-local skills.
- The existing `project-coordinator` documents an older `feat/issue-...` workflow, requires `ai-ready`, cannot spawn subagents, and uses a stale worktree path.
- `AGENTS.md` and `CLAUDE.md` also document the older `feat/issue-...` convention.
- Current MusicShare issue plans already use the global `docs/issues/<number>-<slug>.md` shape.
- No authoritative issue/staging page contract exists for this repository.

## Proposed implementation

Add a self-contained `.codex/skills/issue-delivery` package with the workflow, directly referenced references, and explicit skill metadata. Add the required project-scoped `issue_planner`, `issue_worker`, `issue_reviewer`, and `issue_shipper` TOML agents under `.codex/agents`. Localize all paths and preserve the global model routing, exact-SHA review, merge approval, bounded recovery, and ten-cycle ceiling. Reconcile `AGENTS.md`, `CLAUDE.md`, `docs/06-agents.md`, and the legacy `project-coordinator` guidance so `$issue-delivery` owns the `issue/<number>-<short-kebab-case-slug>` contract without removing the existing specialist skills.

## File-level plan

- `.codex/skills/issue-delivery/SKILL.md`: repository-local issue-delivery workflow and local path/cleanup rules.
- `.codex/skills/issue-delivery/references/planner-workflow.md`: planner state machine, MusicShare base/issue-page/cleanup contract, and exact-SHA gates.
- `.codex/skills/issue-delivery/references/approval-gate-dry-runs.md`: approval and merge safety cases.
- `.codex/skills/issue-delivery/agents/openai.yaml`: explicit-only skill metadata.
- `.codex/agents/issue-planner.toml`: repository-scoped planner role.
- `.codex/agents/issue-worker.toml`: repository-scoped implementation role.
- `.codex/agents/issue-reviewer.toml`: repository-scoped read-only exact-HEAD reviewer.
- `.codex/agents/issue-shipper.toml`: repository-scoped PR preparation and separately authorized merge role.
- `AGENTS.md`, `CLAUDE.md`, `docs/06-agents.md`: document the new workflow and its explicit precedence over the legacy branch convention.
- `.codex/skills/project-coordinator/SKILL.md`: mark the older coordinator as legacy and direct current issue delivery to `$issue-delivery`, avoiding contradictory active instructions.

## Validation plan

- Parse every project-scoped agent TOML and assert the four required names and required fields.
- Search the repository-local package and guidance for stale absolute global paths and contradictory active branch/merge instructions.
- Verify every local skill reference resolves and the approval-gate cases remain represented.
- Run `git diff --check`, the repository-supported frontend/backend CI-equivalent checks appropriate for docs/config-only changes, and inspect the complete diff against `origin/main`.
- Independently review the exact implementation HEAD before creating the PR.

## Risks and edge cases

- Do not touch the existing issue/101 checkout or the two prunable MusicShare worktrees.
- Do not invent an issue/staging URL.
- Do not delete local or remote branches merely because the linked worktree is removed.
- A changed HEAD invalidates the reviewer approval and requires a fresh review.

## Definition of done

- [ ] Repository-local skill, references, metadata, and four project-scoped agents are checked in without absolute user paths.
- [ ] MusicShare guidance has no unresolved conflict for the new issue-delivery workflow.
- [ ] Existing specialist skills remain intact.
- [ ] The exact implementation HEAD receives an independent approval.
- [ ] A PR targets `main`, references `Closes #103`, and reports validation accurately.
- [ ] The task-owned worktree is removed after PR delivery; unrelated worktrees and branches remain intact.
