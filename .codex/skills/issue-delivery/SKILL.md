---
name: issue-delivery
description: Use when the user requests the gated issue-delivery workflow for a repository change or resumes its review, shipping, or approved merge.
---

# Issue Delivery

`Top level -> issue_planner -> issue_worker <-> issue_reviewer -> issue_shipper`

## MusicShare repository contract

This repository's current issue-delivery contract is repository-local and applies
to every invocation of this skill:

- Repository: `BaileyMillerSSI/MusicShare`; remote: `origin`; base/default branch: `main`.
- Fetch `origin/main` and create each fresh issue worktree from that ref before implementation. Record the task-owned linked-worktree path and use it for every phase.
- The immutable issue branch form is `issue/<issue-number>-<short-kebab-case-slug>`; never substitute another prefix or rename it.
- Pull requests target `main`, reference `Closes #<issue-number>`, and cannot be merged without a new explicit user approval for the exact ready PR.
- MusicShare has no authoritative issue/staging page contract. Declare `ISSUE_PAGE: NONE` unless repository guidance later provides an authoritative HTTPS discovery method; never invent a URL.
- After `issue_shipper` returns `READY_FOR_APPROVAL`, `issue_planner` (the top-level coordinator) must independently verify the PR readiness and then remove only the exact linked worktree created for the task after verifying its path and branch identity. This cleanup is required for PR-only delivery and is not deferred until merge. Preserve local/remote branches and all existing or unrelated worktrees; `issue_shipper` only reports readiness and never performs this cleanup.
- A later approved merge resumes from the PR and remote branch state after cleanup; the removed worktree is not required. If source repair is needed, create a fresh task-owned worktree and route the change through the worker and fresh exact-HEAD review.

## Top-level dispatch

The original chat is a lightweight relay. Delegate planning and delivery coordination to the repository-local `issue_planner`; do not investigate the repository, define the issue, write the plan, review source, or run delivery mechanics here. The named agents' TOML files select their model and reasoning effort; never override them or rely on the chat's model settings.

On first dispatch, use `agent_type: "issue_planner"`, `fork_turns: "none"`, and a compact packet containing:

- repository/workspace path or the supplied repository identifier
- the user's requested outcome and exact acceptance constraints, including relevant earlier decisions
- existing issue/PR/plan references when supplied
- authorized scope and explicit pause, research-only, target-branch, or approval constraints
- the repository-local skill path `.codex/skills/issue-delivery/SKILL.md`

Preserve essential user facts that are not stored elsewhere; avoid forwarding conversation history. Let the planner resolve repository facts. If a required named role or context isolation is unavailable, report the blocker without substituting the top-level model or another role.

Retain only the planner agent ID, current phase, issue/PR references, approved HEAD, and pending user decision. Relay concise progress and blockers. On follow-up, resume that planner with only the new user instruction and changed constraints; do not resend the original packet. Pause requests must be forwarded immediately and stop all downstream work.

At `READY_FOR_APPROVAL`, independently verify the shipper's PR, checks, reviewed HEAD, and issue page if present, perform the exact task-owned worktree cleanup, then show those details and yield. A new explicit user instruction identifying the ready PR is required to merge. Forward that instruction verbatim with the PR and approved HEAD to the same planner. Neither elapsed time nor the original delivery request is approval.

## Context and agent reuse contract

Every new role instance uses `fork_turns: "none"`. Initial downstream packets contain the worktree, phase, issue/plan/base/branch references, relevant SHA, and only phase-specific inputs. Reference durable artifacts rather than copying plans, source, diffs, test logs, or other agents' narratives. Role rules already in TOML need not be repeated in dispatch prompts.

Reuse the same worker for implementation/fix rounds, the same independent reviewer for subsequent reviews, and the same shipper for preparation and a separately authorized merge turn. Use `followup_task` for an idle agent and messaging for a running agent. Follow-ups contain the next phase, current/previous SHA as relevant, new findings or user decisions, and changed constraints/reference paths only. Agents reread changed artifacts and verify current Git/GitHub state; prior context never substitutes for current exact-SHA evidence.

Create a replacement only if the old agent is unavailable, its context is lost, capacity requires retirement, or the bounded authentication retry requires it. Reconstruct with identity/reference fields, current phase/SHA, unresolved findings, retry counters, and current authorization scope; do not replay the transcript. Never reuse a worker as reviewer. Each changed HEAD requires a new reviewer verdict even when the reviewer instance is reused.

## Planner-only instructions

`issue_planner` reads [references/planner-workflow.md](references/planner-workflow.md) and executes the delivery state machine. Other roles read only their assigned issue, plan, repository guidance, and phase-relevant artifacts. The top level does not load this reference during normal delivery.

Return to the top level only:

```text
STATUS: IN_PROGRESS | READY_FOR_APPROVAL | MERGED | BLOCKED
ISSUE: <number + URL or NONE>
PLAN: <path or NONE>
PR: <number + URL or NONE>
HEAD: <reviewed/approved SHA or NONE>
CHECKS: PASS | FAIL | NONE | INDETERMINATE
ISSUE_PAGE: <HTTPS URL or NONE>
MERGE_SHA: <SHA or NONE>
NEXT: <one concise action, user decision, or blocker>
```

Keep detailed evidence with the responsible role or in durable artifacts. Preserve the 10-cycle review/fix ceiling, independent review, branch protections, and explicit merge-approval gate.
