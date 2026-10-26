import { readFile, writeFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";
import { normalizeDotnet } from './dotnet-audit-summary.mjs';
export const expectedProjects = ['Api', 'AppHost', 'Contracts', 'Persistence', 'Services', 'ServiceDefaults', 'Tests'].map(name => `MusicShare.${name}/MusicShare.${name}.csproj`);
const zero = () => ({ critical: 0, high: 0, moderate: 0, low: 0, info: 0 });
export function normalizeReports(
    audit,
    semgrep,
    identity,
    auditExit,
    staticExit,
    expectedRepository = "BaileyMillerSSI/MusicShare",
    dotnet = null,
    dotnetExit = -1,
) {
    const dependency = { status: "unknown", counts: zero() },
        scan = { status: "unknown", counts: zero() };
    if (
        [0, 1].includes(auditExit) &&
        !audit?.error &&
        audit?.auditReportVersion === 2 &&
        audit?.metadata?.vulnerabilities
    ) {
        const counts = audit.metadata.vulnerabilities;
        if (
            Object.keys(dependency.counts).every(
                (key) =>
                    Number.isSafeInteger(counts[key]) &&
                    counts[key] >= 0 &&
                    counts[key] <= 1000000,
            )
        ) {
            dependency.counts = Object.fromEntries(
                Object.keys(dependency.counts).map((key) => [key, counts[key]]),
            );
            dependency.status = auditExit === 0 && Object.values(dependency.counts).some((count) => count > 0) ? "unknown" : "completed";
        }
    }
    if (
        [0, 1].includes(staticExit) &&
        semgrep?.version === "1.182.0" &&
        Array.isArray(semgrep.results) &&
        Array.isArray(semgrep.errors) &&
        Array.isArray(semgrep.paths?.scanned) &&
        semgrep.paths.scanned.length > 0
    ) {
        scan.status = semgrep.errors.length || semgrep.paths.skipped?.length ? "partial" : "completed";
        if (semgrep.results.length > 1000000) scan.status = "unknown";
        for (const finding of semgrep.results) {
            const severity = {
                ERROR: "high",
                WARNING: "moderate",
                INFO: "info",
            }[finding?.extra?.severity];
            if (!severity) {
                scan.status = "unknown";
                break;
            }
            scan.counts[severity]++;
            if (scan.counts[severity] > 1000000) scan.status = "unknown";
        }
    }
    if (
        !/^[a-f0-9]{40}$/.test(identity.sha) ||
        !Number.isSafeInteger(identity.run_id) ||
        identity.run_id < 1 ||
        identity.repository !== expectedRepository ||
        !/^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/.test(expectedRepository)
    )
        throw new Error("Invalid scan identity");
    const completed_at = new Date().toISOString();
    const tool = (id, name, kind, result, version, coverage) => ({
        id,
        tool: name,
        tool_version: typeof version === "string" && /^[A-Za-z0-9.+_-]{1,64}$/.test(version) ? version : "unknown",
        kind,
        status: result.status,
        counts: result.status === "unknown" ? null : result.counts,
        completed_at: result.status === "unknown" ? null : completed_at,
        coverage,
    });
    return {
        version: 2,
        repository: identity.repository,
        sha: identity.sha,
        run_id: identity.run_id,
        workflow: "ops-security.yml",
        completed_at,
        tools: [
            tool("dotnet-solution", "dotnet-list-package", "dependency", normalizeDotnet(dotnet, dotnetExit, expectedProjects), identity.dotnet_version, ["MusicShare.slnx: all seven projects, direct and transitive dependency advisory occurrences"]),
            tool(
                "npm-frontend",
                "npm-audit",
                "dependency",
                dependency,
                identity.npm_version,
                ["MusicShare.Frontend production and development dependencies"],
            ),
            tool("semgrep-js", "semgrep", "static", scan, semgrep?.version, [
                "semgrep/semgrep-rules@16b9e6d45f10649f6a531ec4838ed9924372c3bc: detect-eval-with-expression, detect-insecure-websocket",
            ]),
        ],
    };
}
if (
    process.argv[1] &&
    import.meta.url === pathToFileURL(process.argv[1]).href
) {
    const folder = process.argv[2] || ".private/security";
    const read = async (name) => {
        try {
            return JSON.parse(await readFile(`${folder}/${name}.json`, "utf8"));
        } catch {
            return null;
        }
    };
    const plain = async (name) => {
        try { return (await readFile(`${folder}/${name}`, "utf8")).trim(); } catch { return ""; }
    };
    const exit = async (name) => { const value = await plain(`${name}.exit`); return /^[0-9]+$/.test(value) ? Number(value) : null; };
    const summary = normalizeReports(
        await read("audit"),
        await read("semgrep"),
        {
            repository: process.env.GITHUB_REPOSITORY,
            npm_version: await plain("npm.version"),
            dotnet_version: (await readFile(`${folder}/dotnet.version`, "utf8").catch(() => "unknown")).trim(),
            sha: process.env.SCANNED_SHA,
            run_id: Number(process.env.GITHUB_RUN_ID),
        },
        await exit("audit"),
        await exit("semgrep"),
        "BaileyMillerSSI/MusicShare",
        await read("dotnet"),
        await exit("dotnet"),
    );
    await writeFile(
        `${folder}/security-summary.json`,
        JSON.stringify(summary, null, 2) + "\n",
    );
}
