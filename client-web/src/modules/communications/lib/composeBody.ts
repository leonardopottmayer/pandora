import { Marked } from 'marked'
import DOMPurify from 'dompurify'

const parser = new Marked({ gfm: true, breaks: true })

/**
 * The HTML part of an outgoing message, also what the preview shows. A quote can carry whatever the original's sender wrote,
 * so images go too: in the preview a remote one would be a tracking pixel firing, and inline images are not supported.
 */
export function markdownToHtml(markdown: string): string {
  const raw = parser.parse(markdown, { async: false })
  return DOMPurify.sanitize(raw, { FORBID_TAGS: ['img', 'style', 'form', 'input', 'iframe'], FORBID_ATTR: ['style'] })
}

export function quoteForReply(heading: string, text: string): string {
  const quoted = text
    .trimEnd()
    .split(/\r?\n/)
    .map((line) => `> ${line}`)
    .join('\n')
  return `\n\n${heading}\n${quoted}`
}

/** `crypto.randomUUID` only exists in a secure context, and a homelab may be plain http. */
export function newSendKey(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16))
  bytes[6] = (bytes[6] & 0x0f) | 0x40
  bytes[8] = (bytes[8] & 0x3f) | 0x80
  const hex = [...bytes].map((b) => b.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

export interface StoredDraft {
  mailboxId?: string
  to?: string[]
  cc?: string[]
  bcc?: string[]
  subject?: string
  body?: string
}

const draftKey = (id: string) => `mail.compose.draft.${id}`

export function loadDraft(id: string): StoredDraft | null {
  try {
    const stored = localStorage.getItem(draftKey(id))
    return stored ? (JSON.parse(stored) as StoredDraft) : null
  } catch {
    return null
  }
}

export function saveDraft(id: string, draft: StoredDraft): void {
  try {
    localStorage.setItem(draftKey(id), JSON.stringify(draft))
  } catch {
    // A draft is a convenience: without storage the form still works.
  }
}

export function clearDraft(id: string): void {
  try {
    localStorage.removeItem(draftKey(id))
  } catch {
    // Same as above.
  }
}
