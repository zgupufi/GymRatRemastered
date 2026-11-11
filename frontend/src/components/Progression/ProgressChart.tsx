import { useEffect, useId, useRef, useState } from 'react'
import { WeightUnit } from '../../api/auth'
import type { ProgressPoint } from '../../api/progress'
import { displayWeight, unitLabel } from '../../lib/weight'
import { formatDate, formatFullDate, niceDomain, pickLabelIndexes, smoothPath } from './chartMath'
import './ProgressChart.css'

const DEFAULT_WIDTH = 880
const LEFT = 40
const RIGHT = 20
const TOP = 24
const BOTTOM = 40
const MAX_LABELS = 5

interface ProgressChartProps {
  exerciseName: string
  points: ProgressPoint[]
  unit?: WeightUnit
}

function useWidth() {
  const ref = useRef<HTMLDivElement>(null)
  const [width, setWidth] = useState(DEFAULT_WIDTH)
  useEffect(() => {
    const element = ref.current
    if (!element || typeof ResizeObserver === 'undefined') {
      return
    }
    const observer = new ResizeObserver(([entry]) => {
      const next = Math.round(entry.contentRect.width)
      if (next > 0) {
        setWidth(next)
      }
    })
    observer.observe(element)
    return () => observer.disconnect()
  }, [])
  return { ref, width }
}

export function ProgressChart({ exerciseName, points: rawPoints, unit = WeightUnit.Kilograms }: ProgressChartProps) {
  const points = rawPoints.map((point) => ({
    ...point,
    estimatedOneRepMax: displayWeight(point.estimatedOneRepMax, unit),
    bestSet: { ...point.bestSet, weightKg: displayWeight(point.bestSet.weightKg, unit) },
  }))
  const label = unitLabel(unit).toLowerCase()
  const gradientId = useId()
  const { ref, width } = useWidth()
  const height = width < 520 ? 240 : 320
  const [active, setActive] = useState(points.length - 1)

  const values = points.map((point) => point.estimatedOneRepMax)
  const domain = niceDomain(values)
  const plotWidth = width - LEFT - RIGHT
  const plotHeight = height - TOP - BOTTOM
  const scaleX = (index: number) => LEFT + (index / (points.length - 1)) * plotWidth
  const scaleY = (value: number) => TOP + (1 - (value - domain.min) / (domain.max - domain.min)) * plotHeight
  const coordinates = values.map((value, index) => ({ x: scaleX(index), y: scaleY(value) }))
  const line = smoothPath(coordinates)
  const area = `${line} L${coordinates[coordinates.length - 1].x},${TOP + plotHeight} L${coordinates[0].x},${TOP + plotHeight}Z`
  const current = points[active]
  const anchor = coordinates[active]

  const select = (event: React.PointerEvent<SVGRectElement>) => {
    const box = event.currentTarget.getBoundingClientRect()
    const ratio = box.width === 0 ? 1 : (event.clientX - box.left) / box.width
    setActive(Math.min(points.length - 1, Math.max(0, Math.round(ratio * (points.length - 1)))))
  }

  return (
    <div className="progress-chart" ref={ref}>
      <div className="progress-chart__tooltip" aria-live="polite">
        <span className="progress-chart__tooltip-date">{formatFullDate(current.date)}</span>
        <strong>
          {current.estimatedOneRepMax} <small>{label}</small>
        </strong>
        <span className="progress-chart__tooltip-label">Estimated 1RM</span>
        <span className="progress-chart__tooltip-set">
          {current.bestSet.weightKg} {label} × {current.bestSet.reps} reps
        </span>
      </div>
      <svg
        viewBox={`0 0 ${width} ${height}`}
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
            <line className="progress-chart__grid" x1={LEFT} x2={width - RIGHT} y1={scaleY(tick)} y2={scaleY(tick)} />
            <text className="progress-chart__tick" x={0} y={scaleY(tick) + 4}>
              {tick}
            </text>
          </g>
        ))}
        {pickLabelIndexes(points.length, width < 520 ? 3 : MAX_LABELS).map((index) => (
          <text
            key={points[index].date}
            className="progress-chart__tick"
            x={scaleX(index)}
            y={height - 12}
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
            aria-label={`${formatFullDate(point.date)}, ${point.estimatedOneRepMax} ${label} estimated one rep max`}
            onFocus={() => setActive(index)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault()
                setActive(index)
              }
            }}
          />
        ))}
        <rect
          className="progress-chart__surface"
          x={LEFT}
          y={0}
          width={plotWidth}
          height={height}
          onPointerMove={select}
          onPointerDown={select}
        />
      </svg>
    </div>
  )
}
