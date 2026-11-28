const DAY_MS = 24 * 60 * 60 * 1000

export const HISTORY_DAYS = 56

const SKIPPED_SLOTS = [9, 22]

const STALLED_SESSIONS = 3

const exercise = (name, targetSets, repsMin, repsMax, weightStepKg, startWeightKg, ending = 'progress') => ({
  name,
  targetSets,
  repsMin,
  repsMax,
  weightStepKg,
  startWeightKg,
  ending,
})

export const WORKOUTS = [
  {
    name: 'Upper A',
    day: 1,
    exercises: [
      exercise('Bench Press', 3, 6, 8, 2.5, 60, 'increase'),
      exercise('Barbell Rows', 3, 8, 10, 2.5, 50),
      exercise('Overhead Press', 3, 6, 8, 2.5, 35, 'stall'),
      exercise('Pull-ups', 3, 5, 12, 1.25, 0),
      exercise('Triceps Pushdowns', 3, 10, 12, 1.25, 20),
    ],
  },
  {
    name: 'Lower A',
    day: 2,
    exercises: [
      exercise('Squats', 3, 5, 7, 5, 80),
      exercise('Romanian Deadlift (RDL)', 3, 8, 10, 2.5, 70),
      exercise('Leg Press', 3, 10, 12, 5, 120),
      exercise('Machine Calf Raises', 3, 12, 15, 2.5, 40, 'hold'),
    ],
  },
  {
    name: 'Upper B',
    day: 4,
    exercises: [
      exercise('Incline Dumbbell Press', 3, 8, 10, 2, 22),
      exercise('Lat Pulldown (Wide Grip)', 3, 8, 12, 2.5, 45),
      exercise('Seated Cable Row', 3, 10, 12, 2.5, 45),
      exercise('Lateral Raises', 3, 12, 15, 1, 8, 'stall'),
      exercise('Hammer Curls', 3, 10, 12, 2, 12),
    ],
  },
  {
    name: 'Lower B',
    day: 5,
    exercises: [
      exercise('Deadlift', 3, 4, 6, 5, 100),
      exercise('Bulgarian Split Squats', 3, 8, 10, 2, 14),
      exercise('Seated Leg Curls', 3, 10, 12, 2.5, 35),
      exercise('Cable Crunches', 3, 12, 15, 1.25, 25),
    ],
  },
]

export function isoWeekday(date) {
  return ((date.getUTCDay() + 6) % 7) + 1
}

export function buildSchedule(now) {
  const today = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate())
  const slots = []
  for (let daysAgo = HISTORY_DAYS; daysAgo >= 1; daysAgo--) {
    const day = new Date(today - daysAgo * DAY_MS)
    const workout = WORKOUTS.find((candidate) => candidate.day === isoWeekday(day))
    if (workout) {
      slots.push({ workout, day })
    }
  }
  const kept = slots.filter((_, index) => !SKIPPED_SLOTS.includes(index))
  return kept.map(({ workout, day }, index) => {
    const startedAt = new Date(day.getTime() + (17 * 60 + ((index * 7) % 45)) * 60 * 1000)
    const durationSeconds = (48 + ((index * 11) % 23)) * 60
    return {
      workout,
      startedAt,
      finishedAt: new Date(startedAt.getTime() + durationSeconds * 1000),
      durationSeconds,
    }
  })
}

export function performSets(settings, target, occurrence, totalOccurrences) {
  const weightKg = target.weightKg ?? settings.startWeightKg
  const sets = (repsFor, reserveFor) =>
    Array.from({ length: settings.targetSets }, (_, index) => ({
      reps: repsFor(index),
      weightKg,
      repsInReserve: reserveFor(index),
    }))

  if (settings.ending === 'stall' && occurrence >= totalOccurrences - STALLED_SESSIONS) {
    return sets(() => settings.repsMin - 1, () => 0)
  }

  if (settings.ending === 'increase' && occurrence === totalOccurrences - 1) {
    return sets(() => settings.repsMax, () => 2)
  }

  const base = target.reason === 'NoHistory' ? settings.repsMin + 1 : target.reps
  if (settings.ending === 'hold' && occurrence === totalOccurrences - 1) {
    return sets(() => base, () => 0)
  }

  const isOffDay = occurrence % 5 === 3
  const lastIndex = settings.targetSets - 1
  return sets(
    (index) => {
      if (index === 0 && occurrence % 2 === 0) {
        return Math.min(base + 1, Math.max(settings.repsMax, base))
      }
      return index === lastIndex && isOffDay ? Math.max(base - 1, settings.repsMin) : base
    },
    (index) => (index === lastIndex ? 1 : 2),
  )
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

function uuid(value) {
  if (!UUID.test(value)) {
    throw new Error(`Expected a GUID, got "${value}".`)
  }
  return `'${value}'::uuid`
}

const timestamp = (date) => `'${date.toISOString()}'::timestamptz`

export function buildBackdateSql(sessions, workoutIds, createdAt) {
  const rows = sessions
    .map((session) => `(${uuid(session.id)}, ${timestamp(session.startedAt)}, ${timestamp(session.finishedAt)}, ${session.durationSeconds})`)
    .join(',\n')
  const ids = workoutIds.map(uuid).join(', ')
  return `BEGIN;
UPDATE "WorkoutSessions" AS session
SET "StartedAt" = demo.started_at, "FinishedAt" = demo.finished_at, "DurationSeconds" = demo.duration_seconds
FROM (VALUES
${rows}
) AS demo (id, started_at, finished_at, duration_seconds)
WHERE session."Id" = demo.id;
UPDATE "Workouts" SET "CreatedAt" = ${timestamp(createdAt)} WHERE "Id" IN (${ids});
UPDATE "Users" SET "CreatedAt" = ${timestamp(createdAt)} WHERE "Id" IN (SELECT "UserId" FROM "Workouts" WHERE "Id" IN (${ids}));
COMMIT;
`
}
