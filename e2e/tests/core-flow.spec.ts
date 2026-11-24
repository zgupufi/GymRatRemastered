import { expect, test, type Page } from '@playwright/test'

const PASSWORD = 'Smoke-Test-Passw0rd!'
const FIRST_EXERCISE = 'Bench Press'
const SECOND_EXERCISE = 'Incline Dumbbell Press'
const DECIMAL_WEIGHT = '62.5'
const SECOND_WEIGHT = '22.5'

function uniqueEmail(projectName: string): string {
  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
  return `smoke-${projectName}-${suffix}@example.com`
}

async function goTo(page: Page, name: string) {
  await page.getByRole('link', { name, exact: true }).filter({ visible: true }).first().click()
}

async function addExercise(page: Page, name: string) {
  await page.getByRole('textbox', { name: 'Search exercises' }).fill(name)
  await page.getByRole('button', { name: `Add ${name}`, exact: true }).click()
  await expect(page.getByRole('button', { name: `Remove ${name}`, exact: true })).toBeVisible()
}

async function logSet(page: Page, weight: string, reps: string) {
  await page.getByRole('textbox', { name: 'Set 1 weight' }).fill(weight)
  await page.getByRole('spinbutton', { name: 'Set 1 reps' }).fill(reps)
  const saved = page.waitForResponse(
    (response) => response.url().includes('/exercises/') && response.url().endsWith('/sets') && response.request().method() === 'PUT',
  )
  await page.getByRole('button', { name: 'Complete set 1' }).click()
  expect((await saved).ok()).toBe(true)
}

test('register, plan, train, resume and review progress', async ({ page }, testInfo) => {
  page.on('dialog', (dialog) => void dialog.accept())

  await page.goto('/register')
  await page.getByLabel('First name').fill('Smoke')
  await page.getByLabel('Email').fill(uniqueEmail(testInfo.project.name))
  await page.getByLabel('Password').fill(PASSWORD)
  await page.getByRole('button', { name: 'Create account' }).click()
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  await expect(page).toHaveURL(/\/$/)

  await goTo(page, 'Workouts')
  await page.getByRole('button', { name: 'Create workout' }).click()
  await addExercise(page, FIRST_EXERCISE)
  await addExercise(page, SECOND_EXERCISE)
  for (const name of [FIRST_EXERCISE, SECOND_EXERCISE]) {
    await page.getByRole('spinbutton', { name: `Sets for ${name}` }).fill('1')
  }
  await expect(page.getByRole('status').filter({ hasText: 'Saved' })).toBeVisible()

  await goTo(page, 'Split')
  const dayName = await page.evaluate(() => new Date().toLocaleDateString('en-US', { weekday: 'long' }))
  await page.getByRole('button', { name: `${dayName} workout` }).click()
  await page.getByRole('option').filter({ hasText: 'New workout' }).click()
  await page.getByRole('button', { name: 'Save split' }).click()
  await expect(page.getByText('Split saved')).toBeVisible()

  await goTo(page, 'Home')
  await expect(page.getByRole('list', { name: /Exercises in today/ })).toContainText(FIRST_EXERCISE)
  await page.getByRole('link', { name: 'Start workout' }).click()
  await page.getByRole('button', { name: 'Start workout' }).click()
  await expect(page.getByText('Exercise 1 of 2')).toBeVisible()
  await expect(page.getByRole('heading', { name: FIRST_EXERCISE })).toBeVisible()

  await logSet(page, DECIMAL_WEIGHT, '10')
  await expect(page.getByText('Exercise 2 of 2')).toBeVisible()
  await expect(page.getByRole('heading', { name: SECOND_EXERCISE })).toBeVisible()
  await logSet(page, SECOND_WEIGHT, '12')

  await page.reload()
  await expect(page.getByText('Exercise 2 of 2')).toBeVisible()
  await expect(page.getByRole('textbox', { name: 'Set 1 weight' })).toHaveValue(SECOND_WEIGHT)
  await expect(page.getByRole('button', { name: 'Undo set 1' })).toBeVisible()
  await page.getByRole('button', { name: 'Previous' }).click()
  await expect(page.getByRole('textbox', { name: 'Set 1 weight' })).toHaveValue(DECIMAL_WEIGHT)
  await page.getByRole('button', { name: 'Next' }).click()

  await page.getByRole('button', { name: 'Finish' }).click()
  await expect(page.getByRole('heading', { name: 'Workout finished' })).toBeVisible()
  await expect(page.getByRole('list', { name: 'Best set per exercise' })).toContainText(`${DECIMAL_WEIGHT} kg × 10`)

  await page.getByRole('link', { name: 'View progression' }).click()
  await page.getByRole('button', { name: FIRST_EXERCISE, exact: true }).click()
  await expect(page.getByText(`${DECIMAL_WEIGHT} kg × 10`)).toBeVisible()
})
