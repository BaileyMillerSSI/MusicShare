# Manual security summary draft

`ops-security.yml` is dispatch-only. Do not run it, add a schedule or activate integrations until separately approved. This draft has been tested with fixtures only; no scan results or production safety claims are implied.

The workflow uses npm audit for frontend production/development dependencies, `dotnet package list --vulnerable --include-transitive --format json --output-version 1` for the solution, and pinned Semgrep Community Edition with two pinned upstream JavaScript/TypeScript rules (eval expressions and insecure WebSockets). This small static ruleset does not inspect C# and is not comprehensive application coverage; C# static coverage remains unknown. The normalizers only convert these established tool outputs to security-summary v2; they are not vulnerability scanners. .NET counts are advisory occurrences per package/framework, so repeated dependencies may be counted more than once. Missing projects/frameworks, warnings/problems, malformed output and failed commands are unknown, never a clean zero. A report that omits explicit frameworks is conservatively unknown.

Only `security-summary.json` is uploaded in `ops-security-${sha}`. Raw output remains on the ephemeral runner, never in artifacts. The summary binds GitHub repository, exact checked-out SHA and run ID; it does not claim that SHA is deployed. No Azure management access, production tokens or `/status` requests are involved.

Reference: [Microsoft dotnet package list](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list). Tool and coverage labels in each report are the authority for the limited checks performed; completed means the stated checks completed, not that the application is safe.
