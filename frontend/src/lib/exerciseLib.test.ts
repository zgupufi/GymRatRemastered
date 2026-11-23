import { describe, expect, it } from 'vitest'
import { difficultyTone } from './difficulty'
import { muscleLabel, musclesLabel } from './exerciseLabels'
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

describe('muscleLabel', () => {
  it.each([
    ['Triceps', 'Triceps'],
    ['FrontDeltoids', 'Front Deltoids'],
    ['LongHeadOfBiceps', 'Long Head of Biceps'],
  ])('formats %s as %s', (input, label) => {
    expect(muscleLabel(input)).toBe(label)
  })

  it('joins several muscles and returns an empty string for none', () => {
    expect(musclesLabel(['Glutes', 'LowerBack'])).toBe('Glutes, Lower Back')
    expect(musclesLabel([])).toBe('')
  })
})
