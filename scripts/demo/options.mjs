import { parseArgs } from 'node:util'

const LOCAL_HOSTS = ['localhost', '127.0.0.1', '[::1]']

export const USAGE = `Usage: node scripts/demo/seed.mjs [options]

  --api <url>               API on this machine (default http://localhost:8080)
  --email <email>           demo account email (default demo@gymrat.test)
  --password <password>     demo account password (default GymRat-Demo-1)
  --reset                   delete the demo account first, with all its data
  --docker "<command>"      how to call Docker (default docker, for example "wsl -d Ubuntu docker")
  --compose-project <name>  Compose project name, if the stack was started with -p
  --help                    show this text`

export function parseOptions(args) {
  const { values } = parseArgs({
    args,
    options: {
      api: { type: 'string', default: 'http://localhost:8080' },
      email: { type: 'string', default: 'demo@gymrat.test' },
      password: { type: 'string', default: 'GymRat-Demo-1' },
      reset: { type: 'boolean', default: false },
      docker: { type: 'string', default: 'docker' },
      'compose-project': { type: 'string' },
      help: { type: 'boolean', default: false },
    },
  })

  const url = new URL(values.api)
  if (!LOCAL_HOSTS.includes(url.hostname)) {
    throw new Error(`The demo seed only runs against a local API, not "${url.hostname}".`)
  }

  const docker = values.docker.split(' ').filter(Boolean)
  if (docker.length === 0) {
    throw new Error('--docker must not be empty.')
  }

  return {
    api: url.origin,
    email: values.email,
    password: values.password,
    reset: values.reset,
    help: values.help,
    docker,
    composeProject: values['compose-project'],
  }
}

export function psqlCommand({ docker, composeProject }) {
  const [command, ...prefix] = docker
  const project = composeProject ? ['--project-name', composeProject] : []
  return {
    command,
    args: [...prefix, 'compose', ...project, 'exec', '-T', 'postgres', 'psql', '-U', 'gymrat', '-d', 'gymrat', '-v', 'ON_ERROR_STOP=1', '-q'],
  }
}
