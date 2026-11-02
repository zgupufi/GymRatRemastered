import type { ReactNode } from 'react'

const BULLET = /^\s*(?:[-*]|\d+\.)\s+(.*)$/

function renderInline(text: string): ReactNode[] {
  return text.split(/(\*\*[^*]+\*\*)/g).map((part, index) =>
    part.startsWith('**') && part.endsWith('**') && part.length > 4 ? (
      <strong key={index}>{part.slice(2, -2)}</strong>
    ) : (
      part
    ),
  )
}

export function formatReply(reply: string): ReactNode {
  const blocks: ReactNode[] = []
  let items: string[] = []

  const flushItems = () => {
    if (items.length > 0) {
      blocks.push(
        <ul key={blocks.length}>
          {items.map((item, index) => (
            <li key={index}>{renderInline(item)}</li>
          ))}
        </ul>,
      )
      items = []
    }
  }

  for (const line of reply.split('\n')) {
    const bullet = BULLET.exec(line)
    if (bullet) {
      items.push(bullet[1])
      continue
    }
    flushItems()
    if (line.trim()) {
      blocks.push(<p key={blocks.length}>{renderInline(line)}</p>)
    }
  }
  flushItems()
  return blocks
}
