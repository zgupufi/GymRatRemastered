import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'

const [resultsDir, thresholdArg = '90'] = process.argv.slice(2)
const threshold = Number(thresholdArg)

function findReports(dir) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) return findReports(path)
    return name === 'coverage.cobertura.xml' ? [path] : []
  })
}

const lines = new Map()
const branches = new Map()

for (const report of findReports(resultsDir)) {
  const xml = readFileSync(report, 'utf8')
  for (const [, filename, body] of xml.matchAll(/<class [^>]*filename="([^"]+)"[^>]*>([\s\S]*?)<\/class>/g)) {
    for (const [, attributes] of body.matchAll(/<line ([^>]*?)\/?>/g)) {
      const number = /number="(\d+)"/.exec(attributes)[1]
      const hits = Number(/hits="(\d+)"/.exec(attributes)[1])
      const key = `${filename}:${number}`
      lines.set(key, Math.max(lines.get(key) ?? 0, hits))

      const condition = /condition-coverage="[^"]*\((\d+)\/(\d+)\)"/.exec(attributes)
      if (condition) {
        const covered = Number(condition[1])
        const total = Number(condition[2])
        const previous = branches.get(key)
        branches.set(key, { total, covered: Math.max(previous?.covered ?? 0, covered) })
      }
    }
  }
}

const lineTotal = lines.size
const lineCovered = [...lines.values()].filter((hits) => hits > 0).length
const branchTotal = [...branches.values()].reduce((sum, b) => sum + b.total, 0)
const branchCovered = [...branches.values()].reduce((sum, b) => sum + b.covered, 0)

const percent = (covered, total) => (total === 0 ? 100 : (covered / total) * 100)
const linePercent = percent(lineCovered, lineTotal)
const branchPercent = percent(branchCovered, branchTotal)

console.log(`Backend line coverage:   ${linePercent.toFixed(2)}% (${lineCovered}/${lineTotal})`)
console.log(`Backend branch coverage: ${branchPercent.toFixed(2)}% (${branchCovered}/${branchTotal})`)

const failures = []
if (linePercent < threshold) failures.push('line')
if (branchPercent < threshold) failures.push('branch')

if (failures.length > 0) {
  const missed = [...lines].filter(([, hits]) => hits === 0).map(([key]) => key)
  const partial = [...branches].filter(([, b]) => b.covered < b.total).map(([key]) => key)
  if (missed.length > 0) console.error(['Uncovered lines:', ...missed].join('\n  '))
  if (partial.length > 0) console.error(['Partially covered branches:', ...partial].join('\n  '))
  console.error(`Backend ${failures.join(' and ')} coverage is below the required ${threshold}%.`)
  process.exit(1)
}
