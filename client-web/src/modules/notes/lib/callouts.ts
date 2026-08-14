import type { TokenizerAndRendererExtension, Tokens } from 'marked'

// Obsidian-flavoured callouts: a blockquote whose first line is `[!type] optional title`. The
// markdown stays a plain blockquote for anything that doesn't know the convention — that is the
// point of the syntax, and the reason we don't invent one of our own.

export const CALLOUT_TYPES = ['note', 'tip', 'info', 'warning', 'danger', 'quote'] as const

export type CalloutType = (typeof CALLOUT_TYPES)[number]

/** Default heading shown when the marker carries no title of its own, per type. */
export type CalloutLabels = Record<CalloutType, string>

const CALLOUT_ICONS: CalloutLabels = {
  note: '✏️',
  tip: '💡',
  info: 'ℹ️',
  warning: '⚠️',
  danger: '🔥',
  quote: '❝',
}

function isCalloutType(value: string): value is CalloutType {
  return (CALLOUT_TYPES as readonly string[]).includes(value)
}

// A run of blockquote lines whose first one opens with the marker. Anything else — including an
// unknown type like `[!frobnicate]` — is left to marked's own blockquote tokenizer.
const CALLOUT_BLOCK = /^ {0,3}> *\[!(\w+)\] *([^\n]*)(?:\n|$)((?: {0,3}>[^\n]*(?:\n|$))*)/

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

interface CalloutToken extends Tokens.Generic {
  type: 'callout'
  calloutType: CalloutType
  title: string
}

/**
 * marked extension rendering `> [!warning] Careful` as a styled block. `labels` supplies the
 * fallback title per type, so a callout written without one reads in the user's language.
 */
export function calloutExtension(labels: CalloutLabels): TokenizerAndRendererExtension {
  return {
    name: 'callout',
    level: 'block',
    start(src) {
      return src.match(/^ {0,3}> *\[!/m)?.index
    },
    tokenizer(src) {
      const match = CALLOUT_BLOCK.exec(src)
      if (!match) return undefined

      const calloutType = match[1].toLowerCase()
      if (!isCalloutType(calloutType)) return undefined

      // Strip the `> ` off every continuation line to get the callout's own markdown.
      const body = match[3].replace(/^ {0,3}> ?/gm, '')
      const token: CalloutToken = {
        type: 'callout',
        raw: match[0],
        calloutType,
        title: match[2].trim(),
        tokens: this.lexer.blockTokens(body),
      }
      return token
    },
    renderer(token) {
      const { calloutType, title } = token as CalloutToken
      const heading = escapeHtml(title || labels[calloutType])
      const body = this.parser.parse(token.tokens ?? [])

      return (
        `<div class="notes-callout notes-callout-${calloutType}">` +
        `<div class="notes-callout-title">` +
        `<span class="notes-callout-icon">${CALLOUT_ICONS[calloutType]}</span>${heading}` +
        `</div>` +
        (body ? `<div class="notes-callout-body">${body}</div>` : '') +
        `</div>`
      )
    },
  }
}
