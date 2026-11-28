import assert from 'node:assert/strict'
import { test } from 'node:test'
import { parseOptions, psqlCommand } from './options.mjs'

test('the defaults point at the local Compose stack', () => {
  const options = parseOptions([])

  assert.equal(options.api, 'http://localhost:8080')
  assert.equal(options.email, 'demo@gymrat.test')
  assert.equal(options.reset, false)
  assert.equal(psqlCommand(options).command, 'docker')
  assert.deepEqual(psqlCommand(options).args.slice(0, 4), ['compose', 'exec', '-T', 'postgres'])
})

test('Docker can be called through WSL with a named Compose project', () => {
  const options = parseOptions(['--docker', 'wsl -d Ubuntu docker', '--compose-project', 'demo', '--reset'])
  const { command, args } = psqlCommand(options)

  assert.equal(command, 'wsl')
  assert.deepEqual(args.slice(0, 8), ['-d', 'Ubuntu', 'docker', 'compose', '--project-name', 'demo', 'exec', '-T'])
  assert.equal(options.reset, true)
})

test('a quoted Docker path keeps its spaces', () => {
  const options = parseOptions(['--docker', '"C:/Program Files/Docker/docker.exe" --context desktop'])

  assert.deepEqual(options.docker, ['C:/Program Files/Docker/docker.exe', '--context', 'desktop'])
})

test('only an API on this machine is accepted', () => {
  assert.equal(parseOptions(['--api', 'http://127.0.0.1:8180/']).api, 'http://127.0.0.1:8180')
  assert.equal(parseOptions(['--api', 'http://[::1]:8080']).api, 'http://[::1]:8080')
  assert.throws(() => parseOptions(['--api', 'https://gymrat.example.com']), /only runs against a local API/)
  assert.throws(() => parseOptions(['--api', 'http://localhost.example.com']), /only runs against a local API/)
})

test('an empty Docker command and unknown options are refused', () => {
  assert.throws(() => parseOptions(['--docker', ' ']), /must not be empty/)
  assert.throws(() => parseOptions(['--force']))
})
