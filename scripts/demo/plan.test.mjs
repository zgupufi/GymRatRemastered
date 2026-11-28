import assert from 'node:assert/strict'
import { test } from 'node:test'
import { HISTORY_DAYS, WORKOUTS, buildBackdateSql, buildSchedule, isoWeekday, performSets } from './plan.mjs'

const settings = { targetSets: 3, repsMin: 6, repsMax: 8, startWeightKg: 60, ending: 'progress' }
const noHistory = { weightKg: null, reps: 6, reason: 'NoHistory' }
const addRep = { weightKg: 62.5, reps: 7, reason: 'AddRep' }
const reps = (sets) => sets.map((set) => set.reps)

test('the plan has four workouts on different days without repeating an exercise', () => {
  const names = WORKOUTS.flatMap((workout) => workout.exercises.map((entry) => entry.name))

  assert.equal(WORKOUTS.length, 4)
  assert.equal(new Set(WORKOUTS.map((workout) => workout.day)).size, 4)
  assert.equal(new Set(names).size, names.length)
})

test('every weekday gives thirty sessions over the last eight weeks, oldest first and before today', () => {
  for (let offset = 0; offset < 7; offset++) {
    const now = new Date(Date.UTC(2026, 2, 2 + offset, 0, 5))
    const today = Date.UTC(2026, 2, 2 + offset)
    const schedule = buildSchedule(now)

    assert.equal(schedule.length, 30)
    assert.ok(schedule.every((slot) => slot.workout.day === isoWeekday(slot.startedAt)))
    assert.ok(schedule.every((slot, index) => index === 0 || slot.startedAt > schedule[index - 1].finishedAt))
    assert.ok(schedule.every((slot) => slot.finishedAt.getTime() < today))
    assert.ok(schedule[0].startedAt.getTime() >= today - HISTORY_DAYS * 24 * 60 * 60 * 1000)
    assert.ok(schedule.every((slot) => slot.finishedAt - slot.startedAt === slot.durationSeconds * 1000))
  }
})

test('the first session starts at the configured weight above the bottom of the range', () => {
  const sets = performSets(settings, noHistory, 0, 8)

  assert.deepEqual(reps(sets), [8, 7, 7])
  assert.ok(sets.every((set) => set.weightKg === 60))
})

test('later sessions follow the served target and keep reps in reserve', () => {
  const sets = performSets(settings, addRep, 1, 8)

  assert.deepEqual(reps(sets), [7, 7, 7])
  assert.deepEqual(sets.map((set) => set.repsInReserve), [2, 2, 1])
  assert.ok(sets.every((set) => set.weightKg === 62.5))
})

test('an off day drops the last set by one rep but not below the range', () => {
  assert.deepEqual(reps(performSets(settings, addRep, 3, 8)), [7, 7, 6])
  assert.deepEqual(reps(performSets(settings, { ...addRep, reps: 6 }, 3, 8)), [6, 6, 6])
})

test('the extra rep on the first set stays inside the range', () => {
  assert.deepEqual(reps(performSets(settings, { ...addRep, reps: 8 }, 2, 8)), [8, 8, 8])
})

test('a stalled exercise misses the range at maximum effort in its last three sessions', () => {
  const stalled = { ...settings, ending: 'stall' }

  assert.ok(performSets(stalled, addRep, 4, 8).every((set) => set.reps >= 6))
  for (const occurrence of [5, 6, 7]) {
    assert.ok(performSets(stalled, addRep, occurrence, 8).every((set) => set.reps === 5 && set.repsInReserve === 0))
  }
})

test('a held exercise reaches maximum effort only in its last session', () => {
  const held = { ...settings, ending: 'hold' }

  assert.ok(performSets(held, addRep, 6, 8).every((set) => set.repsInReserve > 0))
  assert.ok(performSets(held, addRep, 7, 8).every((set) => set.reps === 7 && set.repsInReserve === 0))
})

test('an exercise due for more weight fills the range in its last session', () => {
  const ready = { ...settings, ending: 'increase' }

  assert.deepEqual(reps(performSets(ready, addRep, 6, 8)), [8, 7, 7])
  assert.deepEqual(reps(performSets(ready, addRep, 7, 8)), [8, 8, 8])
})

const id = '01a17eb2-f7ef-71aa-bfae-2e6bebdf12fa'
const session = {
  id,
  startedAt: new Date('2026-01-05T17:00:00Z'),
  finishedAt: new Date('2026-01-05T18:00:00Z'),
  durationSeconds: 3600,
}

test('the backdating SQL moves sessions, workouts and the user in one transaction', () => {
  const sql = buildBackdateSql([session], [id], new Date('2026-01-05T16:00:00Z'))

  assert.match(sql, /^BEGIN;/)
  assert.match(sql, /'2026-01-05T17:00:00\.000Z'::timestamptz, '2026-01-05T18:00:00\.000Z'::timestamptz, 3600/)
  assert.match(sql, /UPDATE "Workouts" SET "CreatedAt" = '2026-01-05T16:00:00\.000Z'/)
  assert.match(sql, /UPDATE "Users"/)
  assert.match(sql, /COMMIT;\n$/)
})

test('the backdating SQL refuses an id that is not a GUID', () => {
  assert.throws(() => buildBackdateSql([{ ...session, id: "1'; DROP TABLE x; --" }], [id], session.startedAt), /Expected a GUID/)
  assert.throws(() => buildBackdateSql([session], ['nope'], session.startedAt), /Expected a GUID/)
})
