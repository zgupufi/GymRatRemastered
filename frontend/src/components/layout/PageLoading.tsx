import { Spinner } from '../ui/Spinner'

export function PageLoading() {
  return (
    <main className="page" aria-busy="true">
      <Spinner />
    </main>
  )
}
