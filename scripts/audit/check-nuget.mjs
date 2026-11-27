import { readFileSync } from 'node:fs'

const blocking = new Set(['high', 'critical'])

const report = JSON.parse(readFileSync(process.argv[2], 'utf8'))

const errors = (report.problems ?? []).filter((problem) => problem.level === 'error')
for (const problem of errors) {
  console.error(problem.text)
}

const findings = new Set()
for (const project of report.projects ?? []) {
  for (const framework of project.frameworks ?? []) {
    for (const item of [...(framework.topLevelPackages ?? []), ...(framework.transitivePackages ?? [])]) {
      for (const vulnerability of item.vulnerabilities ?? []) {
        if (blocking.has(vulnerability.severity.toLowerCase())) {
          findings.add(`${vulnerability.severity}: ${item.id} ${item.resolvedVersion} ${vulnerability.advisoryurl}`)
        }
      }
    }
  }
}

for (const finding of findings) {
  console.error(finding)
}

if (errors.length > 0 || findings.size > 0 || !report.projects?.length) {
  console.error('NuGet audit failed.')
  process.exit(1)
}

console.log('NuGet audit passed: no high or critical vulnerabilities.')
