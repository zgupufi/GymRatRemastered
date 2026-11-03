import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent as ReactKeyboardEvent } from 'react'
import { AnimatePresence, motion } from 'motion/react'
import { ArrowUp, Check, MessageCircle, Sparkles, Trash2, X } from 'lucide-react'
import { MAX_CHAT_MESSAGE_LENGTH } from '../../api/chat'
import { press, spring } from '../../lib/motion'
import { formatReply } from './formatReply'
import { useChat } from './useChat'
import './ChatWidget.css'

const COUNTER_THRESHOLD = 850

const GREETING = "Hi, I'm your GymRat Coach. I can build workouts, change sets and plan your week. What do you need?"

const SUGGESTIONS = [
  'Build a push day',
  'Add 1 set to every exercise',
  'Plan my week, 4 days',
  'Swap squats for leg press',
]

export function ChatWidget() {
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState('')
  const { entries, pending, send, retry, clear } = useChat()
  const listRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLTextAreaElement>(null)

  useEffect(() => {
    const input = inputRef.current
    if (input) {
      input.style.height = 'auto'
      input.style.height = `${input.scrollHeight}px`
    }
  }, [draft, open])

  useEffect(() => {
    const list = listRef.current
    if (list) {
      list.scrollTop = list.scrollHeight
    }
  }, [entries, pending, open])

  useEffect(() => {
    if (!open) {
      return
    }
    inputRef.current?.focus()
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false)
      }
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [open])

  const trimmed = draft.trim()
  const overLimit = draft.length > MAX_CHAT_MESSAGE_LENGTH
  const canSend = trimmed.length > 0 && !overLimit && !pending

  const submit = () => {
    if (!canSend) {
      return
    }
    setDraft('')
    void send(trimmed)
  }

  const onSubmit = (event: FormEvent) => {
    event.preventDefault()
    submit()
  }

  const onInputKeyDown = (event: ReactKeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault()
      submit()
    }
  }

  const ask = (text: string) => {
    if (!pending) {
      void send(text)
    }
  }

  return (
    <>
      <AnimatePresence>
        {open ? (
          <motion.section
            className="coach"
            role="dialog"
            aria-label="GymRat Coach"
            initial={{ opacity: 0, y: 24, scale: 0.96 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 24, scale: 0.96 }}
            transition={spring}
          >
            <header className="coach__header">
              <span className="coach__avatar" aria-hidden="true">
                <Sparkles size={18} />
              </span>
              <div className="coach__title">
                <h2>GymRat Coach</h2>
                <p>
                  <span className="coach__dot" aria-hidden="true" />
                  AI assistant · Online
                </p>
              </div>
              {entries.length > 0 ? (
                <button type="button" className="coach__icon-button" aria-label="Clear chat" onClick={clear} disabled={pending}>
                  <Trash2 size={15} aria-hidden="true" />
                </button>
              ) : null}
              <button type="button" className="coach__icon-button" aria-label="Close chat" onClick={() => setOpen(false)}>
                <X size={16} aria-hidden="true" />
              </button>
            </header>

            <div className="coach__messages" ref={listRef} role="log" aria-live="polite">
              {entries.length === 0 ? (
                <div className="coach__empty">
                  <span className="coach__avatar coach__avatar--large" aria-hidden="true">
                    <Sparkles size={26} />
                  </span>
                  <h3>Your digital trainer</h3>
                  <p>Ask for a workout, tweak your sets or plan your week. I will update your app as we go.</p>
                  <div className="coach__suggestions">
                    {SUGGESTIONS.map((suggestion) => (
                      <button key={suggestion} type="button" className="coach__suggestion" onClick={() => ask(suggestion)}>
                        {suggestion}
                      </button>
                    ))}
                  </div>
                </div>
              ) : null}

              {entries.length > 0 ? (
                <div className="coach__row">
                  <span className="coach__avatar coach__avatar--small" aria-hidden="true">
                    <Sparkles size={13} />
                  </span>
                  <div className="coach__bubble coach__bubble--assistant">{GREETING}</div>
                </div>
              ) : null}

              {entries.map((entry) => {
                if (entry.kind === 'user') {
                  return (
                    <div key={entry.id} className="coach__row coach__row--user">
                      <div className="coach__bubble coach__bubble--user">{entry.content}</div>
                    </div>
                  )
                }
                if (entry.kind === 'error') {
                  return (
                    <div key={entry.id} className="coach__error" role="alert">
                      {entry.content}
                      <button type="button" onClick={() => void retry(entry.id)} disabled={pending}>
                        Retry
                      </button>
                    </div>
                  )
                }
                return (
                  <div key={entry.id} className="coach__group">
                    <div className="coach__row">
                      <span className="coach__avatar coach__avatar--small" aria-hidden="true">
                        <Sparkles size={13} />
                      </span>
                      <div className="coach__bubble coach__bubble--assistant">{formatReply(entry.content)}</div>
                    </div>
                    {entry.workoutsChanged ? (
                      <span className="coach__updated">
                        <Check size={12} strokeWidth={3} aria-hidden="true" />
                        Workouts updated
                      </span>
                    ) : null}
                  </div>
                )
              })}

              {pending ? (
                <div className="coach__row" role="status" aria-label="The coach is typing">
                  <span className="coach__avatar coach__avatar--small" aria-hidden="true">
                    <Sparkles size={13} />
                  </span>
                  <div className="coach__bubble coach__bubble--assistant coach__typing" aria-hidden="true">
                    <i />
                    <i />
                    <i />
                  </div>
                </div>
              ) : null}
            </div>

            <form className="coach__composer" onSubmit={onSubmit}>
              <div className="coach__input">
                <textarea
                  ref={inputRef}
                  rows={1}
                  value={draft}
                  onChange={(event) => setDraft(event.target.value)}
                  onKeyDown={onInputKeyDown}
                  placeholder="Ask the coach…"
                  aria-label="Message the coach"
                  autoComplete="off"
                />
                <motion.button
                  type="submit"
                  className="coach__send"
                  aria-label="Send message"
                  disabled={!canSend}
                  whileTap={canSend ? press : undefined}
                  transition={spring}
                >
                  <ArrowUp size={18} strokeWidth={2.5} aria-hidden="true" />
                </motion.button>
              </div>
              {draft.length >= COUNTER_THRESHOLD ? (
                <p className={`coach__counter${overLimit ? ' coach__counter--over' : ''}`}>
                  {draft.length} / {MAX_CHAT_MESSAGE_LENGTH}
                </p>
              ) : null}
            </form>
          </motion.section>
        ) : null}
      </AnimatePresence>

      <motion.button
        type="button"
        className={`coach__fab${open ? ' coach__fab--open' : ''}`}
        aria-label={open ? 'Close GymRat Coach' : 'Open GymRat Coach'}
        aria-expanded={open}
        onClick={() => setOpen((value) => !value)}
        whileTap={press}
        transition={spring}
      >
        {open ? <X size={24} aria-hidden="true" /> : <MessageCircle size={24} aria-hidden="true" />}
      </motion.button>
    </>
  )
}
