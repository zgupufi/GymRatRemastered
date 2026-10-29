export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export type SortDirection = 'Ascending' | 'Descending'

export interface ListParams<TSort extends string> {
  term?: string
  sortBy?: TSort
  direction?: SortDirection
  page?: number
  pageSize?: number
}

export function toQuery<TSort extends string>(params: ListParams<TSort>): string {
  const query = new URLSearchParams()
  const term = params.term?.trim()
  if (term) {
    query.set('search.term', term)
  }
  if (params.sortBy) {
    query.set('sort.by', params.sortBy)
  }
  if (params.direction) {
    query.set('sort.direction', params.direction)
  }
  if (params.page) {
    query.set('pagination.page', String(params.page))
  }
  if (params.pageSize) {
    query.set('pagination.pageSize', String(params.pageSize))
  }
  const text = query.toString()
  return text ? `?${text}` : ''
}
