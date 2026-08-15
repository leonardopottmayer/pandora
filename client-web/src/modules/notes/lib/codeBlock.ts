import type { RendererObject, Tokens } from 'marked'

// A fenced code block renders as marked's own `<pre><code>`, plus a `data-lang` on the `<pre>` when
// the fence names a language. The CSS turns that attribute into a small label in the block's corner
// — the only visual difference a language makes, on purpose: no token colouring, to stay in the
// monochrome key of the rest of the markdown.

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

export const codeRenderer: RendererObject = {
  code({ text, lang }: Tokens.Code): string {
    const language = (lang ?? '').match(/^\S*/)?.[0] ?? ''
    const body = escapeHtml(text)
    if (!language) {
      return `<pre><code>${body}</code></pre>`
    }
    const escaped = escapeHtml(language)
    return `<pre data-lang="${escaped}"><code class="language-${escaped}">${body}</code></pre>`
  },
}
