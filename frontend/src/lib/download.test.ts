import { afterEach, expect, it, vi } from 'vitest'
import { saveBlob } from './download'

afterEach(() => {
  vi.restoreAllMocks()
})

it('clicks a temporary link to save the file and releases the object url', () => {
  const createObjectURL = vi.fn(() => 'blob:test')
  const revokeObjectURL = vi.fn()
  Object.assign(URL, { createObjectURL, revokeObjectURL })
  const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
    expect(this.download).toBe('data.csv')
    expect(this.href).toBe('blob:test')
  })

  saveBlob(new Blob(['a']), 'data.csv')

  expect(click).toHaveBeenCalledTimes(1)
  expect(revokeObjectURL).toHaveBeenCalledWith('blob:test')
  expect(document.querySelector('a[download]')).toBeNull()
})
