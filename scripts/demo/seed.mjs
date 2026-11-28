import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { createAccount, createClient } from './api.mjs'
import { USAGE, parseOptions, psqlCommand } from './options.mjs'
import { WORKOUTS, buildBackdateSql, buildSchedule, performSets } from './plan.mjs'

const INFRA_DIRECTORY = fileURLToPath(new URL('../../infra', import.meta.url))

function runSql(options, sql) {
  const { command, args } = psqlCommand(options)
  const result = spawnSync(command, args, { cwd: INFRA_DIRECTORY, input: sql, encoding: 'utf8' })
  if (result.error || result.status !== 0) {
    throw new Error(`Could not run SQL in the Compose database: ${result.error?.message ?? result.stderr.trim()}`)
  }
}

async function findExerciseIds(client, names) {
  const ids = new Map()
  for (const name of names) {
    const query = new URLSearchParams({ 'search.term': name, 'pagination.pageSize': '100' })
    const page = await client.send('GET', `/api/exercises?${query}`)
    const match = page.items.find((item) => item.name === name)
    if (!match) {
      throw new Error(`"${name}" is not in the exercise catalog. Has Liquibase run?`)
    }
    ids.set(name, match.id)
  }
  return ids
}

async function createWorkouts(client, exerciseIds) {
  const workoutIds = new Map()
  for (const workout of WORKOUTS) {
    const { id } = await client.send('POST', '/api/workouts', { name: workout.name })
    await client.send('PUT', `/api/workouts/${id}/exercises`, {
      exercises: workout.exercises.map(({ name, targetSets, repsMin, repsMax, weightStepKg }) => ({
        exerciseId: exerciseIds.get(name),
        targetSets,
        repsMin,
        repsMax,
        weightStepKg,
      })),
    })
    await client.send('PUT', `/api/split/${workout.day}`, { workoutId: id })
    workoutIds.set(workout.name, id)
  }
  return workoutIds
}

async function logSessions(client, schedule, workoutIds, exerciseIds) {
  const occurrences = (name) => schedule.filter((slot) => slot.workout.name === name).length
  const done = new Map()
  const sessions = []
  for (const slot of schedule) {
    const { name, exercises } = slot.workout
    const occurrence = done.get(name) ?? 0
    const workoutId = workoutIds.get(name)
    const { id } = await client.send('POST', '/api/sessions', { workoutId })
    const targets = await client.send('GET', `/api/workouts/${workoutId}/targets`)
    for (const settings of exercises) {
      const exerciseId = exerciseIds.get(settings.name)
      const target = targets.find((candidate) => candidate.exerciseId === exerciseId)
      await client.send('PUT', `/api/sessions/${id}/exercises/${exerciseId}/sets`, {
        sets: performSets(settings, target, occurrence, occurrences(name)),
        target: { weightKg: target.weightKg, reps: target.reps, reason: target.reason },
      })
    }
    await client.send('POST', `/api/sessions/${id}/finish`)
    done.set(name, occurrence + 1)
    sessions.push({ ...slot, id })
  }
  return sessions
}

async function verifyDates(client, sessions) {
  const exported = await client.send('GET', '/api/users/me/export')
  const startedAt = new Map(exported.sessions.map((session) => [session.id, Date.parse(session.startedAt)]))
  if (!sessions.every((session) => startedAt.get(session.id) === session.startedAt.getTime())) {
    throw new Error('The session dates did not change. Do the API and the Compose database belong to the same stack?')
  }
}

async function seed(options) {
  const client = createClient(options.api)
  runSql(options, 'SELECT 1 FROM "WorkoutSessions" LIMIT 0;')

  await createAccount(client, options)
  const exerciseIds = await findExerciseIds(client, WORKOUTS.flatMap((workout) => workout.exercises.map((entry) => entry.name)))
  const workoutIds = await createWorkouts(client, exerciseIds)
  const sessions = await logSessions(client, buildSchedule(new Date()), workoutIds, exerciseIds)

  const createdAt = new Date(sessions[0].startedAt.getTime() - 60 * 60 * 1000)
  runSql(options, buildBackdateSql(sessions, [...workoutIds.values()], createdAt))
  await verifyDates(client, sessions)

  console.log(`Seeded ${options.email}: ${workoutIds.size} workouts, a weekly split and ${sessions.length} sessions.`)
  console.log('Log in with the --password value, or with the default shown by --help.')
}

try {
  const options = parseOptions(process.argv.slice(2))
  if (options.help) {
    console.log(USAGE)
  } else {
    await seed(options)
  }
} catch (error) {
  console.error(error.message)
  process.exitCode = 1
}
