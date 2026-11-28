const SETTLE_TIMEOUT_MS = 10_000

let tail: Promise<unknown> = Promise.resolve()

export function enqueueSave(save: () => Promise<void>): Promise<void> {
  const result = tail.then(save)
  tail = result.catch(() => undefined)
  return result
}

export function waitForPendingSaves(): Promise<void> {
  return new Promise((resolve, reject) => {
    const timeout = window.setTimeout(() => reject(new Error('Set saves are still pending')), SETTLE_TIMEOUT_MS)
    void tail.then(() => {
      window.clearTimeout(timeout)
      resolve()
    })
  })
}
