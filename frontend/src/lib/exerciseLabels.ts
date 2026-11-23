const LOWERCASE_WORDS = new Set(['Of'])

export function muscleLabel(muscle: string): string {
  return muscle
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .split(' ')
    .map((word) => (LOWERCASE_WORDS.has(word) ? word.toLowerCase() : word))
    .join(' ')
}

export function musclesLabel(muscles: string[]): string {
  return muscles.map(muscleLabel).join(', ')
}
