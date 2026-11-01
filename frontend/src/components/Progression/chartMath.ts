export interface Point {
  x: number
  y: number
}

export interface Domain {
  min: number
  max: number
  ticks: number[]
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

export function formatDate(date: string): string {
  const [, month, day] = date.split('-').map(Number)
  return `${day} ${MONTHS[month - 1]}`
}

export function formatFullDate(date: string): string {
  return `${formatDate(date)} ${date.slice(0, 4)}`
}

export function niceDomain(values: number[], tickCount = 4): Domain {
  const low = Math.min(...values)
  const high = Math.max(...values)
  const span = Math.max(high - low, 1)
  const rough = (span * 1.2) / (tickCount - 1)
  const magnitude = 10 ** Math.floor(Math.log10(rough))
  const step = [1, 2, 5, 10].map((factor) => factor * magnitude).find((candidate) => candidate >= rough) ?? magnitude
  const min = Math.max(0, Math.floor((low - span * 0.1) / step) * step)
  const ticks: number[] = []
  for (let tick = min; ticks.length < tickCount || tick < high; tick += step) {
    ticks.push(tick)
  }
  return { min, max: ticks[ticks.length - 1], ticks }
}

export function smoothPath(points: Point[]): string {
  if (points.length === 1) {
    return `M${points[0].x},${points[0].y}`
  }
  const slopes = points.slice(1).map((point, index) => (point.y - points[index].y) / (point.x - points[index].x))
  const tangents = points.map((_, index) => {
    if (index === 0) {
      return slopes[0]
    }
    if (index === points.length - 1) {
      return slopes[slopes.length - 1]
    }
    return slopes[index - 1] * slopes[index] <= 0 ? 0 : (slopes[index - 1] + slopes[index]) / 2
  })
  let path = `M${points[0].x},${points[0].y}`
  for (let index = 1; index < points.length; index++) {
    const from = points[index - 1]
    const to = points[index]
    const reach = (to.x - from.x) / 3
    path += ` C${from.x + reach},${from.y + tangents[index - 1] * reach} ${to.x - reach},${to.y - tangents[index] * reach} ${to.x},${to.y}`
  }
  return path
}

export function pickLabelIndexes(count: number, maxLabels: number): number[] {
  if (count <= maxLabels) {
    return Array.from({ length: count }, (_, index) => index)
  }
  const last = count - 1
  return Array.from({ length: maxLabels }, (_, index) => Math.round((index * last) / (maxLabels - 1)))
}
