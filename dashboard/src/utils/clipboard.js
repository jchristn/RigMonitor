export async function copyTextToClipboard(text) {
  if (typeof text !== 'string') {
    throw new TypeError('Clipboard copy expects a string.')
  }

  if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(text)
      return
    } catch {
      // Fall through to a document-based copy path for insecure origins or denied permissions.
    }
  }

  if (typeof document === 'undefined' || typeof document.execCommand !== 'function') {
    throw new Error('Clipboard copy is unavailable in this browser.')
  }

  const textArea = document.createElement('textarea')
  const activeElement = document.activeElement
  const selection = typeof window.getSelection === 'function' ? window.getSelection() : null
  const previousRange = selection && selection.rangeCount > 0 ? selection.getRangeAt(0) : null

  textArea.value = text
  textArea.setAttribute('readonly', '')
  textArea.setAttribute('aria-hidden', 'true')
  textArea.style.position = 'fixed'
  textArea.style.top = '0'
  textArea.style.left = '0'
  textArea.style.width = '1px'
  textArea.style.height = '1px'
  textArea.style.padding = '0'
  textArea.style.border = '0'
  textArea.style.opacity = '0'
  textArea.style.pointerEvents = 'none'

  document.body.appendChild(textArea)
  textArea.focus()
  textArea.select()
  textArea.setSelectionRange(0, text.length)

  try {
    const didCopy = document.execCommand('copy')
    if (!didCopy) {
      throw new Error('Clipboard copy command was rejected.')
    }
  } finally {
    document.body.removeChild(textArea)

    if (selection) {
      selection.removeAllRanges()
      if (previousRange) {
        selection.addRange(previousRange)
      }
    }

    if (activeElement instanceof HTMLElement && typeof activeElement.focus === 'function') {
      activeElement.focus()
    }
  }
}
