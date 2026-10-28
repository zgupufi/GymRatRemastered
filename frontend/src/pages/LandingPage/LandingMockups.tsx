import { ArrowLeft, ArrowUp, Bot, Check, Clock, Dumbbell, Plus, Search, TrendingUp } from 'lucide-react'

type SetState = 'done' | 'current' | 'next'

const sets: { set: number; kg: string; reps: string; state: SetState }[] = [
  { set: 1, kg: '40', reps: '12', state: 'done' },
  { set: 2, kg: '40', reps: '10', state: 'done' },
  { set: 3, kg: '40', reps: '—', state: 'current' },
  { set: 4, kg: '40', reps: '—', state: 'next' },
]

export function PhoneMockup() {
  return (
    <div className="phone" aria-hidden="true">
      <div className="phone__island" />
      <div className="phone__header">
        <span className="phone__back">
          <ArrowLeft size={18} />
        </span>
        <span className="phone__timer">
          <Clock size={14} />
          <span className="timer">00:14</span>
        </span>
      </div>
      <div className="phone__progress">
        <span />
      </div>
      <div className="phone__title">
        <span className="phone__step">Exercise 1 of 11</span>
        <span className="phone__exercise">Pec deck / butterfly</span>
        <span className="phone__chips">
          <span className="chip">Chest</span>
          <span className="chip">Machine</span>
        </span>
      </div>
      <div className="phone__sets">
        <div className="phone__row phone__row--head">
          <span className="phone__set">Set</span>
          <span className="phone__cell">Kg</span>
          <span className="phone__cell">Reps</span>
          <span className="phone__check" />
        </div>
        {sets.map((s) => (
          <div key={s.set} className={`phone__row phone__row--${s.state}`}>
            <span className="phone__set">{s.set}</span>
            <span className="phone__cell">{s.kg}</span>
            <span className={`phone__cell${s.state === 'current' ? ' phone__cell--focus' : ''}`}>{s.reps}</span>
            <span className="phone__check">
              <Check size={16} strokeWidth={3} />
            </span>
          </div>
        ))}
      </div>
      <div className="phone__add">
        <Plus size={14} strokeWidth={2.5} />
        Add set
      </div>
      <div className="phone__spacer" />
      <div className="phone__rest">
        <span className="phone__rest-time">
          <span>Rest</span>
          <span className="timer">01:24</span>
        </span>
        <span className="phone__rest-actions">
          <span>−10s</span>
          <span>+30s</span>
        </span>
      </div>
      <div className="phone__end">End workout</div>
    </div>
  )
}

const exercises = [
  { name: 'Bench press', detail: 'Chest · Barbell', sets: 4 },
  { name: 'Incline dumbbell press', detail: 'Chest · Dumbbell', sets: 3 },
  { name: 'Pec deck / butterfly', detail: 'Chest · Machine', sets: 3 },
  { name: 'Overhead press', detail: 'Shoulders · Barbell', sets: 4 },
  { name: 'Triceps pushdown', detail: 'Triceps · Cable', sets: 3 },
]

export function WorkoutCardMockup() {
  return (
    <div className="mock" aria-hidden="true">
      <div className="mock__heading">
        <span className="mock__title">Push day</span>
        <span className="mock__pill">7 exercises</span>
      </div>
      <div className="mock__search">
        <Search size={16} />
        Search exercises by name...
      </div>
      <div className="mock__list">
        {exercises.map((e, index) => (
          <div key={e.name} className="mock__exercise">
            <span className="mock__index">{index + 1}</span>
            <span className="mock__exercise-text">
              <span className="mock__exercise-name">{e.name}</span>
              <span className="mock__exercise-detail">{e.detail}</span>
            </span>
            <span className="mock__sets">
              <span>Sets</span>
              <strong>{e.sets}</strong>
            </span>
          </div>
        ))}
      </div>
      <div className="mock__footer">
        <span className="button button--primary button--sm">Save changes</span>
      </div>
    </div>
  )
}

const chartWidth = 436
const chartHeight = 220
const chartValues = [92, 95, 94, 99, 103, 102, 108, 111, 110, 115, 118]
const chartTop = 6.5
const chartBottom = chartHeight - 6.5

function chartPoints() {
  return chartValues.map((value, index) => ({
    x: 4 + (index * (chartWidth - 12)) / (chartValues.length - 1),
    y: chartBottom - ((value - 88) / 32) * (chartBottom - chartTop),
  }))
}

function smoothPath(points: { x: number; y: number }[]) {
  const f = (n: number) => n.toFixed(1)
  let d = `M${f(points[0].x)},${f(points[0].y)}`
  for (let i = 0; i < points.length - 1; i++) {
    const p0 = points[i - 1] ?? points[i]
    const p1 = points[i]
    const p2 = points[i + 1]
    const p3 = points[i + 2] ?? p2
    d += ` C${f(p1.x + (p2.x - p0.x) / 6)},${f(p1.y + (p2.y - p0.y) / 6)} ${f(p2.x - (p3.x - p1.x) / 6)},${f(p2.y - (p3.y - p1.y) / 6)} ${f(p2.x)},${f(p2.y)}`
  }
  return d
}

const points = chartPoints()
const line = smoothPath(points)
const last = points[points.length - 1]
const area = `${line} L${last.x.toFixed(1)},${chartBottom} L${points[0].x.toFixed(1)},${chartBottom} Z`
const gridLines = [0, 1, 2, 3, 4].map((i) => chartTop + (i * (chartBottom - chartTop)) / 4)

export function ProgressChartMockup() {
  return (
    <div className="mock" aria-hidden="true">
      <div className="mock__chart-head">
        <span className="mock__chart-title">
          <span className="mock__title">Incline dumbbell press</span>
          <span className="mock__chart-sub">Estimated 1 rep max (kg)</span>
        </span>
        <span className="mock__chart-stat">
          <span className="mock__chart-value">118 kg</span>
          <span className="mock__chart-delta">
            <TrendingUp size={12} strokeWidth={2.5} />
            +14%
          </span>
        </span>
      </div>
      <div className="mock__chart">
        <span className="mock__chart-y">
          {['120', '112', '104', '96', '88'].map((label) => (
            <span key={label}>{label}</span>
          ))}
        </span>
        <svg viewBox={`0 0 ${chartWidth} ${chartHeight}`} className="mock__chart-svg">
          <defs>
            <linearGradient id="landing-chart-fill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stopColor="var(--emerald)" stopOpacity="0.32" />
              <stop offset="1" stopColor="var(--emerald)" stopOpacity="0" />
            </linearGradient>
          </defs>
          {gridLines.map((y) => (
            <line key={y} x1="0" x2={chartWidth} y1={y} y2={y} className="mock__chart-grid" />
          ))}
          <path d={area} fill="url(#landing-chart-fill)" />
          <path d={line} className="mock__chart-line" />
          <circle cx={last.x} cy={last.y} r="10" className="mock__chart-halo" />
          <circle cx={last.x} cy={last.y} r="5" className="mock__chart-dot" />
        </svg>
      </div>
      <div className="mock__chart-x">
        {['01.03', '08.03', '15.03', '22.03', '01.04'].map((label) => (
          <span key={label}>{label}</span>
        ))}
      </div>
    </div>
  )
}

export function CoachChatMockup() {
  return (
    <div className="mock mock--chat" aria-hidden="true">
      <div className="chat__header">
        <span className="chat__avatar">
          <Bot size={20} />
        </span>
        <span className="chat__name">
          <strong>GymRat AI</strong>
          <span className="chat__status">Online</span>
        </span>
      </div>
      <div className="chat__body">
        <span className="chat__bubble chat__bubble--me">Build me a leg day for tomorrow</span>
        <span className="chat__bubble">
          I&apos;ve generated a new <strong>Leg day</strong> routine. It&apos;s ready in your workout list.
        </span>
        <span className="chat__card">
          <span className="chat__card-icon">
            <Dumbbell size={18} />
          </span>
          <span className="chat__card-text">
            <strong>Barbell squats</strong>
            <span>4 sets · 8–10 reps</span>
          </span>
        </span>
        <span className="chat__bubble">Done. Added to your workout!</span>
      </div>
      <div className="chat__input">
        <span>Ask the coach...</span>
        <span className="chat__send">
          <ArrowUp size={18} strokeWidth={2.5} />
        </span>
      </div>
    </div>
  )
}
