import { useEffect, useRef, useState, type FormEvent } from 'react'
import { AnimatePresence, motion } from 'motion/react'
import { ArrowUp, Check, MessageCircle, Sparkles, Trash2, X } from 'lucide-react'
import { MAX_CHAT_MESSAGE_LENGTH } from '../../api/chat'
import { press, spring } from '../../lib/motion'
import { formatReply } from './formatReply'
import { useChat } from './useChat'
import './ChatWidget.css'

const COUNTER_THRESHOLD = 850

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
  const inputRef = useRef<HTMLInputElement>(null)

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

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!canSend) {
      return
    }
    setDraft('')
    void send(trimmed)
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
            className="chat"
            role="dialog"
            aria-label="GymRat Coach"
            initial={{ opacity: 0, y: 24, scale: 0.96 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 24, scale: 0.96 }}
            transition={spring}
          >
            <header className="chat__header">
              <span className="chat__avatar" aria-hidden="true">
                <Sparkles size={18} />
              </span>
              <div className="chat__title">
                <h2>GymRat Coach</h2>
                <p>
                  <span className="chat__dot" aria-hidden="true" />
                  AI assistant · Online
                </p>
              </div>
              {entries.length > 0 ? (
                <button type="button" className="chat__icon-button" aria-label="Clear chat" onClick={clear} disabled={pending}>
                  <Trash2 size={15} aria-hidden="true" />
                </button>
              ) : null}
              <button type="button" className="chat__icon-button" aria-label="Close chat" onClick={() => setOpen(false)}>
                <X size={16} aria-hidden="true" />
              </button>
            </header>

            <div className="chat__messages" ref={listRef} role="log" aria-live="polite">
              {entries.length === 0 ? (
                <div className="chat__empty">
                  <span className="chat__avatar chat__avatar--large" aria-hidden="true">
                    <Sparkles size={26} />
                  </span>
                  <h3>Your digital trainer</h3>
                  <p>Ask for a workout, tweak your sets or plan your week. I will update your app as we go.</p>
                  <div className="chat__suggestions">
                    {SUGGESTIONS.map((suggestion) => (
                      <button key={suggestion} type="button" className="chat__suggestion" onClick={() => ask(suggestion)}>
                        {suggestion}
                      </button>
                    ))}
                  </div>
                </div>
              ) : null}

              {entries.map((entry) => {
                if (entry.kind === 'user') {
                  return (
                    <div key={entry.id} className="chat__row chat__row--user">
                      <div className="chat__bubble chat__bubble--user">{entry.content}</div>
                    </div>
                  )
                }
                if (entry.kind === 'error') {
                  return (
                    <div key={entry.id} className="chat__error" role="alert">
                      {entry.content}
                      <button type="button" onClick={() => void retry(entry.id)} disabled={pending}>
                        Retry
                      </button>
                    </div>
                  )
                }
                return (
                  <div key={entry.id} className="chat__group">
                    <div className="chat__row">
                      <span className="chat__avatar chat__avatar--small" aria-hidden="true">
                        <Sparkles size={13} />
                      </span>
                      <div className="chat__bubble chat__bubble--assistant">{formatReply(entry.content)}</div>
                    </div>
                    {entry.workoutsChanged ? (
                      <span className="chat__updated">
                        <Check size={12} strokeWidth={3} aria-hidden="true" />
                        Workouts updated
                      </span>
                    ) : null}
                  </div>
                )
              })}

              {pending ? (
                <div className="chat__row" role="status" aria-label="The coach is typing">
                  <span className="chat__avatar chat__avatar--small" aria-hidden="true">
                    <Sparkles size={13} />
                  </span>
                  <div className="chat__bubble chat__bubble--assistant chat__typing" aria-hidden="true">
                    <i />
                    <i />
                    <i />
                  </div>
                </div>
              ) : null}
            </div>

            <form className="chat__composer" onSubmit={submit}>
              <div className="chat__input">
                <input
                  ref={inputRef}
                  value={draft}
                  onChange={(event) => setDraft(event.target.value)}
                  placeholder="Ask the coach…"
                  aria-label="Message the coach"
                  autoComplete="off"
                />
                <motion.button
                  type="submit"
                  className="chat__send"
                  aria-label="Send message"
                  disabled={!canSend}
                  whileTap={canSend ? press : undefined}
                  transition={spring}
                >
                  <ArrowUp size={18} strokeWidth={2.5} aria-hidden="true" />
                </motion.button>
              </div>
              {draft.length >= COUNTER_THRESHOLD ? (
                <p className={`chat__counter${overLimit ? ' chat__counter--over' : ''}`}>
                  {draft.length} / {MAX_CHAT_MESSAGE_LENGTH}
                </p>
              ) : null}
            </form>
          </motion.section>
        ) : null}
      </AnimatePresence>

      <motion.button
        type="button"
        className={`chat__fab${open ? ' chat__fab--open' : ''}`}
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
