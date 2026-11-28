import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { gzipSync } from 'node:zlib'

const LIMIT_BYTES = 160 * 1024
const dist = join(import.meta.dirname, '..', 'dist')
const html = readFileSync(join(dist, 'index.html'), 'utf8')
const files = [
  ...html.matchAll(/<script\b[^>]*\bsrc="([^"]+\.js)"/g),
  ...html.matchAll(/<link\b[^>]*\brel="modulepreload"[^>]*\bhref="([^"]+\.js)"/g),
].map((match) => match[1])

if (files.length === 0) {
  console.error('No entry script found in dist/index.html')
  process.exit(1)
}

const kilobytes = (bytes) => `${(bytes / 1024).toFixed(1)} KB`
let total = 0
for (const file of files) {
  const size = gzipSync(readFileSync(join(dist, file))).length
  total += size
  console.log(`${file}: ${kilobytes(size)} gzip`)
}
console.log(`Entry JavaScript: ${kilobytes(total)} gzip (limit ${kilobytes(LIMIT_BYTES)})`)

if (total > LIMIT_BYTES) {
  console.error('The entry JavaScript is over the limit')
  process.exit(1)
}
