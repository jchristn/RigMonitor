import { useEffect, useRef, useState } from 'react'
import { copyTextToClipboard } from '../utils/clipboard'

function CopyIcon() {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24">
      <path
        d="M9 9.75A2.25 2.25 0 0 1 11.25 7.5h7.5A2.25 2.25 0 0 1 21 9.75v7.5a2.25 2.25 0 0 1-2.25 2.25h-7.5A2.25 2.25 0 0 1 9 17.25v-7.5Z"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.75"
      />
      <path
        d="M15 7.5V6.75A2.25 2.25 0 0 0 12.75 4.5h-7.5A2.25 2.25 0 0 0 3 6.75v7.5a2.25 2.25 0 0 0 2.25 2.25H6"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.75"
      />
    </svg>
  )
}

function CheckIcon() {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24">
      <path
        d="m5 13 4 4L19 7"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="2.25"
      />
    </svg>
  )
}

function CloseIcon() {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24">
      <path
        d="M6 6 18 18M18 6 6 18"
        fill="none"
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.85"
      />
    </svg>
  )
}

export function JsonModal({
  closeLabel,
  copiedLabel,
  copyLabel,
  onClose,
  title,
  value,
}) {
  const [copyState, setCopyState] = useState('idle')
  const resetTimerRef = useRef(null)

  useEffect(() => {
    const previousOverflow = document.body.style.overflow
    const handleKeyDown = (event) => {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    document.body.style.overflow = 'hidden'
    window.addEventListener('keydown', handleKeyDown)

    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener('keydown', handleKeyDown)
    }
  }, [onClose])

  useEffect(() => {
    return () => {
      if (resetTimerRef.current) {
        window.clearTimeout(resetTimerRef.current)
      }
    }
  }, [])

  async function handleCopyClick() {
    try {
      await copyTextToClipboard(value)
      setCopyState('copied')

      if (resetTimerRef.current) {
        window.clearTimeout(resetTimerRef.current)
      }

      resetTimerRef.current = window.setTimeout(() => {
        setCopyState('idle')
      }, 1400)
    } catch {
      setCopyState('idle')
    }
  }

  return (
    <div
      aria-modal="true"
      className="modal-overlay"
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          onClose()
        }
      }}
      role="dialog"
    >
      <section className="surface modal-panel json-modal-panel">
        <header className="modal-header">
          <div>
            <h2>{title}</h2>
          </div>
          <div className="modal-actions">
            <button
              aria-label={copyState === 'copied' ? copiedLabel : copyLabel}
              className={`modal-icon-button ${copyState === 'copied' ? 'success' : ''}`.trim()}
              onClick={() => void handleCopyClick()}
              title={copyState === 'copied' ? copiedLabel : copyLabel}
              type="button"
            >
              {copyState === 'copied' ? <CheckIcon /> : <CopyIcon />}
            </button>
            <button
              aria-label={closeLabel}
              className="modal-icon-button"
              onClick={onClose}
              title={closeLabel}
              type="button"
            >
              <CloseIcon />
            </button>
          </div>
        </header>

        <div className="json-modal-body">
          <pre className="json-modal-viewer">{value}</pre>
        </div>
      </section>
    </div>
  )
}
