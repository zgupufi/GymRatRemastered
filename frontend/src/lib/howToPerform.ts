export interface HowToPerform {
  setup: string
  execution: string
}

export function howToPerform(equipment: string | null, muscle: string, description: string | null): HowToPerform {
  const target = muscle.toLowerCase()
  const kind = equipment?.trim().toLowerCase() ?? 'bodyweight'
  let setup: string
  let execution: string
  if (kind.includes('barbell')) {
    setup = `Load the barbell with a weight you can control. Set your grip and stance so your ${target} is set to do the work.`
    execution = `Move the bar through a full range of motion with control, keeping your ${target} engaged, then return to the start without bouncing.`
  } else if (kind.includes('dumbbell')) {
    setup = `Pick a pair of dumbbells you can control and stand or sit with a stable base, keeping your ${target} engaged.`
    execution = `Move the weights through a full range of motion, squeeze your ${target} at the top, and lower slowly.`
  } else if (kind.includes('cable') || kind.includes('machine')) {
    setup = `Adjust the seat or pulley to your height and choose a weight you can control. Brace so your ${target} drives the movement.`
    execution = `Move smoothly through the full range of motion, pause briefly where your ${target} is most contracted, and return slowly.`
  } else if (kind.includes('bodyweight')) {
    setup = `Find a stable surface with enough space and set your body in a strong starting position, ready to use your ${target}.`
    execution = `Move through the full range of motion at a steady tempo, keeping your core tight and your ${target} working throughout.`
  } else {
    setup = `Set up the equipment safely and get into a stable starting position that targets your ${target}.`
    execution = `Move through the full range of motion with control, keeping tension on your ${target} from start to finish.`
  }
  const note = description?.trim()
  return { setup, execution: note ? `${execution} ${note}` : execution }
}
