// Normalize established dotnet list package JSON only; this module does not scan.
export function normalizeDotnet(report, exitCode, expectedProjects, expectedFrameworks) {
  const unknown = { status: 'unknown', counts: null };
  const counts = { critical: 0, high: 0, moderate: 0, low: 0, info: 0 };
  const record = value => value !== null && typeof value === 'object' && !Array.isArray(value);
  const noProblems = value => value?.problems === undefined || (Array.isArray(value.problems) && value.problems.length === 0);
  if (!record(expectedFrameworks) || exitCode !== 0 || report?.version !== 1 || !noProblems(report) || !Array.isArray(report.projects)
      || !Array.isArray(expectedProjects) || expectedProjects.length === 0
      || new Set(expectedProjects).size !== expectedProjects.length || report.projects.length !== expectedProjects.length) return unknown;
  const seen = new Set();
  for (const project of report.projects) {
    const path = typeof project?.path === 'string' ? project.path.replaceAll('\\', '/') : '';
    const expected = expectedProjects.find(name => path === name || path.endsWith(`/${name}`));
    if (!expected || seen.has(expected) || !noProblems(project) || !Array.isArray(project.frameworks) || project.frameworks.length === 0) return unknown;
    seen.add(expected);
    const expectedTargets = expectedFrameworks[expected];
    if (!Array.isArray(expectedTargets) || expectedTargets.length === 0 || !expectedTargets.every(target => typeof target === 'string' && /^net[0-9][a-z0-9.-]*$/.test(target)) || new Set(expectedTargets).size !== expectedTargets.length || project.frameworks.length !== expectedTargets.length) return unknown;
    const frameworks = new Set();
    for (const framework of project.frameworks) {
      if (!record(framework) || typeof framework.framework !== 'string' || !expectedTargets.includes(framework.framework) || frameworks.has(framework.framework) || !noProblems(framework)) return unknown;
      frameworks.add(framework.framework);
      for (const key of ['topLevelPackages', 'transitivePackages']) {
        const packages = Object.hasOwn(framework, key) ? framework[key] : [];
        if (!Array.isArray(packages)) return unknown;
        for (const item of packages) {
          if (!record(item) || typeof item.id !== 'string' || !item.id || typeof item.resolvedVersion !== 'string' || !item.resolvedVersion || !Array.isArray(item.vulnerabilities) || item.vulnerabilities.length === 0) return unknown;
          for (const vulnerability of item.vulnerabilities) {
            const severity = typeof vulnerability?.severity === 'string' ? vulnerability.severity.toLowerCase() : '';
            if (!Object.hasOwn(counts, severity) || !['critical', 'high', 'moderate', 'low'].includes(severity)) return unknown;
            if (++counts[severity] > 1_000_000) return unknown;
          }
        }
      }
    }
  }
  return { status: 'completed', counts };
}
