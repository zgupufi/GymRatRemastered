import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { formatReply } from './formatReply'

describe('formatReply', () => {
  it('renders paragraphs, bold text and bullet lists', () => {
    const { container } = render(<div>{formatReply('Done. Created **Leg Day**:\n- Squat\n* Leg press\n1. Lunge\n\nEnjoy')}</div>)

    expect(container.querySelectorAll('p')).toHaveLength(2)
    expect(container.querySelector('strong')?.textContent).toBe('Leg Day')
    expect(Array.from(container.querySelectorAll('li')).map((item) => item.textContent)).toEqual([
      'Squat',
      'Leg press',
      'Lunge',
    ])
  })

  it('leaves unmatched asterisks as plain text', () => {
    const { container } = render(<div>{formatReply('5 stars ** and more')}</div>)

    expect(container.querySelector('strong')).toBeNull()
    expect(container.textContent).toBe('5 stars ** and more')
  })
})
