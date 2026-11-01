import { describe, expect, it } from 'vitest'
import { difficultyTone } from './difficulty'
import { howToPerform } from './howToPerform'
import { youtubeEmbedUrl } from './youtube'

describe('difficultyTone', () => {
  it.each([
    ['Beginner', 'beginner'],
    [' intermediate ', 'intermediate'],
    ['ADVANCED', 'advanced'],
    ['Expert', 'neutral'],
    [null, 'neutral'],
  ])('maps %s to %s', (input, tone) => {
    expect(difficultyTone(input)).toBe(tone)
  })
})

describe('youtubeEmbedUrl', () => {
  it.each([
    ['https://www.youtube.com/watch?v=abc123', 'https://www.youtube.com/embed/abc123'],
    ['https://youtu.be/abc123', 'https://www.youtube.com/embed/abc123'],
    ['https://m.youtube.com/watch?v=abc123', 'https://www.youtube.com/embed/abc123'],
    ['https://www.youtube.com/embed/abc123', 'https://www.youtube.com/embed/abc123'],
  ])('converts %s', (link, expected) => {
    expect(youtubeEmbedUrl(link)).toBe(expected)
  })

  it.each([[null], [''], ['not a url'], ['https://example.com/watch?v=abc'], ['https://www.youtube.com/watch']])(
    'returns null for %s',
    (link) => {
      expect(youtubeEmbedUrl(link)).toBeNull()
    },
  )
})

describe('howToPerform', () => {
  it.each([
    ['Barbell', 'Load the barbell'],
    ['Dumbbell', 'dumbbells'],
    ['Cable', 'pulley'],
    ['Machine', 'pulley'],
    ['Bodyweight', 'stable surface'],
    [null, 'stable surface'],
    ['Kettlebell', 'Set up the equipment'],
  ])('writes the setup for %s equipment', (equipment, text) => {
    expect(howToPerform(equipment, 'Chest', null).setup).toContain(text)
  })

  it('mentions the muscle and appends the description to the execution', () => {
    const steps = howToPerform('Barbell', 'Chest', ' Keep your back flat. ')

    expect(steps.setup).toContain('chest')
    expect(steps.execution.endsWith('Keep your back flat.')).toBe(true)
  })

  it('leaves the execution alone when there is no description', () => {
    expect(howToPerform('Dumbbell', 'Back', '  ').execution.endsWith('slowly.')).toBe(true)
  })
})
