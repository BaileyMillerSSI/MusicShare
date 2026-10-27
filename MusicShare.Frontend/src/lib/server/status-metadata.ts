// Imported only by the authenticated Node route; never expose build inputs through NEXT_PUBLIC variables.
export function statusBuild(value: { sha?: unknown; builtAt?: unknown; workflow?: unknown }) {
  const sha = typeof value.sha === 'string' && /^[a-f0-9]{40}(?![\s\S])/.test(value.sha) ? value.sha : null;
  const builtAt = typeof value.builtAt === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z(?![\s\S])/.test(value.builtAt)
    && Number.isFinite(Date.parse(value.builtAt)) && new Date(value.builtAt).toISOString() === value.builtAt ? value.builtAt : null;
  const workflow = value.workflow as { runId?: unknown; url?: unknown } | undefined;
  const runId = workflow?.runId;
  const url = workflow?.url;
  const validWorkflow = typeof runId === 'string' && /^[1-9][0-9]{0,31}(?![\s\S])/.test(runId)
    && typeof url === 'string' && url === `https://github.com/BaileyMillerSSI/MusicShare/actions/runs/${runId}`;
  return { sha, version: null, builtAt, ...(validWorkflow ? { workflow: { runId, url } } : {}) };
}
