# Security summary v2

Artifact `ops-security-${sha}`, exact single file `security-summary.json`:

```json
{"version":2,"repository":"owner/repo","sha":"0000000000000000000000000000000000000000","run_id":1,"workflow":"ops-security.yml","completed_at":"2026-10-26T00:00:00.000Z","tools":[{"id":"npm-root","tool":"npm-audit","tool_version":"actual-version","kind":"dependency","status":"unknown","counts":null,"completed_at":null,"coverage":["root production and development dependencies"]}]}
```

Repository/SHA/run/workflow bind to trusted GitHub metadata. ≤16 unique tool IDs (slug ≤64); tools: npm-audit, dotnet-list-package, semgrep, codeql. Actual tool_version ≤64 or `unknown`; kind dependency/static. Status completed/partial/unknown/error/unsupported. Counts null for unknown/error/unsupported; otherwise exact critical/high/moderate/low/info nonnegative integers ≤1,000,000. Completed/partial require actual completion timestamp. Coverage ≤20 strings, each ≤256 chars. Aggregate ≤64 KiB. No raw findings, paths with secrets, personal data or tool exceptions.

Each package project may have a separate tool entry. Missing tools/output, malformed JSON, transport failures, unverified entitlement and incomplete coverage are not zero findings. Partial counts remain explicitly incomplete. Completed does not mean zero findings. Only established npm/.NET audit and entitled CodeQL or pinned Semgrep CE with established vetted rules are supported; normalization is not a custom scanner. Keep scans separate from `/status`, manual-only until activation approval. Static scan absence keeps deployment coverage unknown. A matching scanned SHA is only limited check coverage, not proof of safety. Never equate CI SHA with deployed SHA without genuine application/provider provenance.
