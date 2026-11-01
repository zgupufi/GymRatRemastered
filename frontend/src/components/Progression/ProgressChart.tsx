import { useId, useState } from 'react'
import type { ProgressPoint } from '../../api/progress'
import { formatDate, formatFullDate, niceDomain, pickLabelIndexes, smoothPath } from './chartMath'
import './ProgressChart.css'

const WIDTH = 880
const HEIGHT = 320
const LEFT = 48
const RIGHT = 20
const TOP = 24
const BOTTOM = 40
const MAX_LABELS = 5
const TOOLTIP_WIDTH = 184

interface ProgressChartProps {
  exerciseName: string
  points: ProgressPoint[]
}

export function ProgressChart({ exerciseName, points }: ProgressChartProps) {
  const gradientId = useId()
  const [active, setActive] = useState(points.length - 1)

  const values = points.map((point) => point.estimatedOneRepMax)
  const domain = niceDomain(values)
  const plotWidth = WIDTH - LEFT - RIGHT
  const plotHeight = HEIGHT - TOP - BOTTOM
  const scaleX = (index: number) => LEFT + (index / (points.length - 1)) * plotWidth
  const scaleY = (value: number) => TOP + (1 - (value - domain.min) / (domain.max - domain.min)) * plotHeight
  const coordinates = values.map((value, index) => ({ x: scaleX(index), y: scaleY(value) }))
  const line = smoothPath(coordinates)
  const area = `${line} L${coordinates[coordinates.length - 1].x},${TOP + plotHeight} L${coordinates[0].x},${TOP + plotHeight}Z`
  const current = points[active]
  const anchor = coordinates[active]
  const tooltipLeft = anchor.x / WIDTH
  const flip = anchor.x > WIDTH * 0.6

  const select = (event: React.PointerEvent<SVGRectElement>) => {
    const box = event.currentTarget.getBoundingClientRect()
    const ratio = box.width === 0 ? 1 : (event.clientX - box.left) / box.width
    setActive(Math.min(points.length - 1, Math.max(0, Math.round(ratio * (points.length - 1)))))
  }

  return (
    <div className="progress-chart">
      <svg
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        role="img"
        aria-label={`Estimated one rep max of ${exerciseName} over ${points.length} sessions`}
      >
        <defs>
          <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stopColor="var(--emerald)" stopOpacity="0.38" />
            <stop offset="1" stopColor="var(--emerald)" stopOpacity="0" />
          </linearGradient>
        </defs>
        {domain.ticks.map((tick) => (
          <g key={tick}>
            <line className="progress-chart__grid" x1={LEFT} x2={WIDTH - RIGHT} y1={scaleY(tick)} y2={scaleY(tick)} />
            <text className="progress-chart__tick" x={0} y={scaleY(tick) + 4}>
              {tick}
            </text>
          </g>
        ))}
        {pickLabelIndexes(points.length, MAX_LABELS).map((index) => (
          <text
            key={points[index].date}
            className="progress-chart__tick"
            x={scaleX(index)}
            y={HEIGHT - 12}
            textAnchor={index === 0 ? 'start' : index === points.length - 1 ? 'end' : 'middle'}
          >
            {formatDate(points[index].date)}
          </text>
        ))}
        <path className="progress-chart__area" d={area} fill={`url(#${gradientId})`} />
        <path className="progress-chart__line" d={line} />
        <line className="progress-chart__cursor" x1={anchor.x} x2={anchor.x} y1={TOP} y2={TOP + plotHeight} />
        <circle className="progress-chart__halo" cx={anchor.x} cy={anchor.y} r={12} />
        <circle className="progress-chart__dot" cx={anchor.x} cy={anchor.y} r={6} />
        {points.map((point, index) => (
          <circle
            key={point.date}
            className="progress-chart__focus"
            cx={coordinates[index].x}
            cy={coordinates[index].y}
            r={10}
            tabIndex={0}
            role="button"
            aria-label={`${formatFullDate(point.date)}, ${Math.round(point.estimatedOneRepMax)} kg estimated one rep max`}
            onFocus={() => setActive(index)}
          />
        ))}
        <rect
          className="progress-chart__surface"
          x={LEFT}
          y={0}
          width={plotWidth}
          height={HEIGHT}
          onPointerMove={select}
          onPointerDown={select}
        />
      </svg>
      <div
        className="progress-chart__tooltip"
        style={{
          left: `calc(${tooltipLeft * 100}% - ${flip ? TOOLTIP_WIDTH + 16 : -16}px)`,
          top: 0,
        }}
        aria-live="polite"
      >
        <span className="progress-chart__tooltip-date">{formatFullDate(current.date)}</span>
        <strong>
          {Math.round(current.estimatedOneRepMax)} <small>kg</small>
        </strong>
        <span className="progress-chart__tooltip-label">Estimated 1RM</span>
        <hr />
        <span className="progress-chart__tooltip-label">Best set</span>
        <span className="progress-chart__tooltip-set">
          {current.bestSet.weightKg} kg × {current.bestSet.reps} reps
        </span>
      </div>
    </div>
  )
}
