import { chipOf, newBlockId, plainText, tagsOf, type Block, type Inline, type Mark, type TextInline } from './blocks'

// A page to and from Markdown (GitHub flavour). Export writes what Markdown can say and falls back
// to plain text for the rest (a sub-page list, a template button); import reads the common syntax:
// headings, lists and to-dos with nesting, quotes and callouts, code and Mermaid, tables, images,
// rules, math, and inline bold, italic, strike, code, links and [[page]] mentions.

type TitleOf = (pageId: string) => string | undefined

// ---------- Export ----------

const wrap: Record<string, [string, string]> = { bold: ['**', '**'], italic: ['*', '*'], strike: ['~~', '~~'], code: ['`', '`'], underline: ['<u>', '</u>'] }

function inlineMarkdown(content: Inline[] = [], titleOf: TitleOf): string {
  return content
    .map((inline) => {
      if ('mention' in inline) return `[[${titleOf(inline.mention.page) ?? inline.mention.page}]]`
      const chip = chipOf(inline)
      if (chip) {
        if (chip.kind === 'math') return `$${(chip.data as { latex: string }).latex}$`
        if (chip.kind === 'status') return `\`${(chip.data as { label: string }).label}\``
        return plainText([inline])
      }
      const text = inline as TextInline
      // Spaces stay outside the marks: `* agora*` is not emphasis in Markdown.
      const [, lead, core, trail] = /^(\s*)([\s\S]*?)(\s*)$/.exec(text.text)!
      let out = core.replace(/([*_~`[\]\\])/g, '\\$1').replace(/\n/g, '  \n')
      for (const mark of core ? (text.marks ?? []) : []) {
        if (mark.type === 'link') out = `[${out}](${String(mark.attrs?.href ?? '')})`
        else if (wrap[mark.type]) out = wrap[mark.type][0] + out + wrap[mark.type][1]
      }
      return lead + out + trail
    })
    .join('')
}

const indented = (text: string, prefix: string) => text.split('\n').map((line) => (line ? prefix + line : line.trimEnd() ? prefix : prefix.trimEnd())).join('\n')

export function blocksToMarkdown(blocks: Block[], titleOf: TitleOf = () => undefined): string {
  const tags = tagsOf(blocks)
  const body = render(blocks, titleOf)
  return (tags.length ? `${tags.map((tag) => `#${tag}`).join(' ')}\n\n` : '') + body.trim() + '\n'
}

function render(blocks: Block[], titleOf: TitleOf): string {
  const parts: string[] = []
  let number = 0
  for (const b of blocks) {
    number = b.type === 'numbered' ? number + 1 : 0
    const text = inlineMarkdown(b.content, titleOf)
    const props = b.props ?? {}
    const children = b.children?.length ? render(b.children, titleOf) : ''
    const list = (marker: string) => `${marker}${text}${children ? '\n' + indented(children, ' '.repeat(marker.length)) : ''}`
    let out: string | null
    switch (b.type) {
      case 'paragraph': out = text; break
      case 'ai': out = inlineMarkdown([{ text: escape(plainText(props.output as Inline[] | undefined)) }], titleOf); break
      case 'agenda-list': out = '[Agenda]'; break
      case 'finance-summary': out = '[Finances]'; break
      case 'heading': out = `${'#'.repeat(Number(props.level ?? 1))} ${text}${children ? '\n\n' + children : ''}`; break
      case 'bulleted': out = list('- '); break
      case 'numbered': out = list(`${number}. `); break
      case 'todo': out = list(`- [${props.checked ? 'x' : ' '}] `); break
      case 'quote': out = indented(text + (children ? '\n\n' + children : ''), '> '); break
      case 'code': out = '```' + String(props.language ?? '') + '\n' + plainText(b.content) + '\n```'; break
      case 'mermaid': out = '```mermaid\n' + String(props.code ?? '') + '\n```'; break
      case 'math': out = `$$\n${String(props.latex ?? '')}\n$$`; break
      case 'divider': out = '---'; break
      case 'callout': out = indented(`[!${String(props.tone ?? 'note').toUpperCase()}]${props.title ? ' ' + String(props.title) : ''}\n${children}`, '> '); break
      case 'toggle': out = `<details>\n<summary>${text}</summary>\n\n${children}\n</details>`; break
      case 'decision': out = `**[${String(props.status ?? 'open')}]** ${text}`; break
      case 'table': {
        const rows = ((props.rows as Inline[][][] | undefined) ?? []).map((row) => `| ${row.map((cell) => inlineMarkdown(cell, titleOf).replace(/\|/g, '\\|')).join(' | ')} |`)
        if (rows.length) rows.splice(1, 0, `|${' --- |'.repeat(((props.rows as unknown[][])[0] ?? []).length)}`)
        out = rows.join('\n')
        break
      }
      case 'properties':
        out = ['| | |', '| --- | --- |', ...((props.rows as { key?: Inline[]; value?: Inline[] }[] | undefined) ?? []).map((row) => `| ${plainText(row.key)} | ${plainText(row.value)} |`)].join('\n')
        break
      case 'image': out = `![${plainText(b.content)}](${String(props.src ?? `pandora-media:${String(props.mediaId)}`)})`; break
      case 'gallery': out = ((props.items as { mediaId: string; caption?: string }[] | undefined) ?? []).map((item) => `![${item.caption ?? ''}](pandora-media:${item.mediaId})`).join('\n'); break
      case 'file':
      case 'audio':
      case 'pdf':
        out = props.url ? `<${String(props.url)}>` : `[${String(props.name ?? b.type)}](pandora-media:${String(props.mediaId)})`
        break
      case 'bookmark':
      case 'embed':
      case 'video':
        out = props.url ? (props.title ? `[${String(props.title)}](${String(props.url)})` : `<${String(props.url)}>`) : null
        break
      case 'page-link': out = `[[${titleOf(String(props.pageId)) ?? String(props.pageId)}]]`; break
      case 'columns':
      case 'column':
      case 'synced':
        out = children || null
        break
      default: out = null
    }
    if (out !== null && out !== '') parts.push(out)
    else if (b.type === 'paragraph') parts.push('')
  }
  return parts.join('\n\n').replace(/\n{3,}/g, '\n\n')
}

// ---------- Export: HTML ----------

const escape = (text: string) => text.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!)
const safeHref = (href: string) => (/^(https?:|mailto:|#)/i.test(href) ? href : '#')

function inlineHtml(content: Inline[] = [], titleOf: TitleOf): string {
  return content
    .map((inline) => {
      if ('mention' in inline) return `<span class="mention">${escape(titleOf(inline.mention.page) ?? '?')}</span>`
      if (chipOf(inline)) return `<span class="chip">${escape(plainText([inline]))}</span>`
      const text = inline as TextInline
      let out = escape(text.text).replace(/\n/g, '<br>')
      for (const mark of text.marks ?? []) {
        const tag = ({ bold: 'strong', italic: 'em', strike: 's', code: 'code', underline: 'u' } as Record<string, string>)[mark.type]
        if (mark.type === 'link') out = `<a href="${escape(safeHref(String(mark.attrs?.href ?? '')))}">${out}</a>`
        else if (tag) out = `<${tag}>${out}</${tag}>`
      }
      return out
    })
    .join('')
}

/**
 * A page as a standalone HTML document. `imageOf` answers an address for a library item (a data URL,
 * so the file carries its images), or null to leave it out.
 */
export async function blocksToHtml(title: string, blocks: Block[], titleOf: TitleOf, imageOf: (mediaId: string) => Promise<string | null>): Promise<string> {
  const html = async (list: Block[]): Promise<string> => {
    const out: string[] = []
    let listTag: string | null = null
    for (const b of list) {
      const props = b.props ?? {}
      const text = inlineHtml(b.content, titleOf)
      const children = b.children?.length ? await html(b.children) : ''
      const tag = b.type === 'bulleted' || b.type === 'todo' ? 'ul' : b.type === 'numbered' ? 'ol' : null
      if (tag !== listTag) {
        if (listTag) out.push(`</${listTag}>`)
        if (tag) out.push(`<${tag}>`)
        listTag = tag
      }
      const image = async (mediaId: unknown, src: unknown, alt: string) => {
        const url = typeof src === 'string' && /^https?:/.test(src) ? src : typeof mediaId === 'string' ? await imageOf(mediaId) : null
        return url ? `<figure><img src="${escape(url)}" alt="${escape(alt)}">${alt ? `<figcaption>${escape(alt)}</figcaption>` : ''}</figure>` : ''
      }
      switch (b.type) {
        case 'paragraph': out.push(`<p>${text}</p>`); break
        case 'ai': out.push(`<div class="ai">${inlineHtml(props.output as Inline[] | undefined, titleOf)}</div>`); break
        case 'agenda-list': out.push('<p>[Agenda]</p>'); break
        case 'finance-summary': out.push('<p>[Finances]</p>'); break
        case 'heading': out.push(`<h${props.level ?? 1}>${text}</h${props.level ?? 1}>${children}`); break
        case 'bulleted':
        case 'numbered': out.push(`<li>${text}${children}</li>`); break
        case 'todo': out.push(`<li><input type="checkbox" disabled${props.checked ? ' checked' : ''}> ${text}${children}</li>`); break
        case 'quote': out.push(`<blockquote>${text}${children}</blockquote>`); break
        case 'code':
        case 'mermaid': out.push(`<pre><code>${escape(b.type === 'mermaid' ? String(props.code ?? '') : plainText(b.content))}</code></pre>`); break
        case 'math': out.push(`<pre class="math">${escape(String(props.latex ?? ''))}</pre>`); break
        case 'divider': out.push('<hr>'); break
        case 'callout': out.push(`<aside class="callout ${escape(String(props.tone ?? 'note'))}">${props.title ? `<strong>${escape(String(props.title))}</strong>` : ''}${children}</aside>`); break
        case 'toggle': out.push(`<details open><summary>${text}</summary>${children}</details>`); break
        case 'decision': out.push(`<p><strong>[${escape(String(props.status ?? 'open'))}]</strong> ${text}</p>`); break
        case 'table': {
          const rows = (props.rows as Inline[][][] | undefined) ?? []
          out.push(`<table>${rows.map((row, i) => `<tr>${row.map((cell) => (props.header && i === 0 ? `<th>${inlineHtml(cell, titleOf)}</th>` : `<td>${inlineHtml(cell, titleOf)}</td>`)).join('')}</tr>`).join('')}</table>`)
          break
        }
        case 'properties':
          out.push(`<table>${((props.rows as { key?: Inline[]; value?: Inline[] }[] | undefined) ?? []).map((row) => `<tr><th>${escape(plainText(row.key))}</th><td>${escape(plainText(row.value))}</td></tr>`).join('')}</table>`)
          break
        case 'image': out.push(await image(props.mediaId, props.src, plainText(b.content))); break
        case 'gallery':
          for (const item of (props.items as { mediaId: string; caption?: string }[] | undefined) ?? []) out.push(await image(item.mediaId, null, item.caption ?? ''))
          break
        case 'bookmark':
        case 'embed':
        case 'video':
        case 'audio':
        case 'pdf':
        case 'file':
          if (props.url) out.push(`<p><a href="${escape(safeHref(String(props.url)))}">${escape(String(props.title ?? props.url))}</a></p>`)
          else if (props.name) out.push(`<p>📎 ${escape(String(props.name))}</p>`)
          break
        case 'page-link': out.push(`<p><span class="mention">${escape(titleOf(String(props.pageId)) ?? '?')}</span></p>`); break
        case 'columns': out.push(`<div class="columns">${children}</div>`); break
        case 'column': out.push(`<div>${children}</div>`); break
        case 'synced': out.push(children); break
      }
    }
    if (listTag) out.push(`</${listTag}>`)
    return out.join('\n')
  }

  const style = 'body{max-width:760px;margin:40px auto;padding:0 16px;font:16px/1.65 system-ui,sans-serif;color:#1f1f1f}img{max-width:100%}table{border-collapse:collapse}td,th{border:1px solid #ddd;padding:4px 8px}blockquote,aside{margin:1em 0;padding:4px 14px;border-left:3px solid #ccc}pre{background:#f5f5f5;padding:12px;overflow:auto}.columns{display:grid;grid-auto-flow:column;gap:24px}.mention,.chip{padding:0 4px;border:1px solid #ddd;border-radius:4px}'
  return `<!doctype html>\n<html><head><meta charset="utf-8"><title>${escape(title)}</title><style>${style}</style></head>\n<body>\n<h1>${escape(title)}</h1>\n${await html(blocks)}\n</body></html>\n`
}

// ---------- Import ----------

function block(type: string, extra: Partial<Block> = {}): Block {
  const made: Block = { id: newBlockId(), type, ...extra }
  if (!made.content?.length) delete made.content
  if (!made.children?.length) delete made.children
  if (!made.props) delete made.props
  return made
}

const indentOf = (line: string) => /^(\s*)/.exec(line)![1].length

/** Inline Markdown as inline content; `[[Title]]` becomes a mention when `pageByTitle` knows the page. */
export function parseInline(text: string, pageByTitle: (title: string) => string | undefined = () => undefined): Inline[] {
  const out: Inline[] = []
  const push = (value: string, marks: Mark[] = []) => {
    if (!value) return
    const last = out.at(-1)
    if (last && 'text' in last && JSON.stringify(last.marks ?? []) === JSON.stringify(marks)) last.text += value
    else out.push(marks.length ? { text: value, marks } : { text: value })
  }
  const pattern = /\\([*_~`[\]\\$])|\[\[([^\]]+)\]\]|\[([^\]]+)\]\(([^)\s]+)\)|`([^`]+)`|\*\*([^*]+)\*\*|__([^_]+)__|~~([^~]+)~~|\*([^*]+)\*|_([^_]+)_|\$([^$\s][^$]*)\$/g
  let at = 0
  for (const match of text.matchAll(pattern)) {
    push(text.slice(at, match.index))
    at = match.index! + match[0].length
    const [, escaped, wiki, linkText, href, code, bold1, bold2, strike, italic1, italic2, math] = match
    if (escaped) push(escaped)
    else if (wiki) {
      const page = pageByTitle(wiki.trim())
      if (page) out.push({ mention: { page } })
      else push(wiki)
    } else if (linkText) push(linkText, [{ type: 'link', attrs: { href } }])
    else if (code) push(code, [{ type: 'code' }])
    else if (bold1 ?? bold2) push(bold1 ?? bold2, [{ type: 'bold' }])
    else if (strike) push(strike, [{ type: 'strike' }])
    else if (italic1 ?? italic2) push(italic1 ?? italic2, [{ type: 'italic' }])
    else if (math) out.push({ math: { latex: math } })
  }
  push(text.slice(at))
  return out
}

const TONES: Record<string, string> = { NOTE: 'note', TIP: 'info', INFO: 'info', IMPORTANT: 'info', SUCCESS: 'success', WARNING: 'warning', CAUTION: 'error', DANGER: 'error', ERROR: 'error' }

/** Markdown as blocks, and the title when the text opens with a level-1 heading. */
export function markdownToBlocks(markdown: string, pageByTitle?: (title: string) => string | undefined): { title: string | null; blocks: Block[] } {
  const lines = markdown.replace(/\r\n?/g, '\n').split('\n')
  const blocks = parseLines(lines, pageByTitle)
  const first = blocks[0]
  if (first?.type === 'heading' && first.props?.level === 1) return { title: plainText(first.content), blocks: blocks.slice(1) }
  return { title: null, blocks }
}

function parseLines(lines: string[], pageByTitle?: (title: string) => string | undefined): Block[] {
  const out: Block[] = []
  const inline = (text: string) => parseInline(text.trim(), pageByTitle)
  let i = 0
  while (i < lines.length) {
    const line = lines[i]
    if (!line.trim()) {
      i++
      continue
    }
    // Fenced code (and Mermaid).
    const fence = /^\s*(```|~~~)\s*([\w+-]*)/.exec(line)
    if (fence) {
      const body: string[] = []
      for (i++; i < lines.length && !lines[i].trim().startsWith(fence[1]); i++) body.push(lines[i])
      i++
      const code = body.join('\n')
      out.push(fence[2] === 'mermaid' ? block('mermaid', { props: { code } }) : block('code', { props: fence[2] ? { language: fence[2] } : undefined, content: code ? [{ text: code }] : undefined }))
      continue
    }
    if (line.trim() === '$$') {
      const body: string[] = []
      for (i++; i < lines.length && lines[i].trim() !== '$$'; i++) body.push(lines[i])
      i++
      out.push(block('math', { props: { latex: body.join('\n') } }))
      continue
    }
    const heading = /^(#{1,6})\s+(.*?)\s*#*\s*$/.exec(line)
    if (heading) {
      out.push(block('heading', { props: { level: Math.min(heading[1].length, 3) }, content: inline(heading[2]) }))
      i++
      continue
    }
    if (/^\s*([-*_])(\s*\1){2,}\s*$/.test(line)) {
      out.push(block('divider'))
      i++
      continue
    }
    // Quotes, and GitHub callouts (> [!WARNING] Title).
    if (/^\s*>/.test(line)) {
      const body: string[] = []
      for (; i < lines.length && /^\s*>/.test(lines[i]); i++) body.push(lines[i].replace(/^\s*>\s?/, ''))
      const callout = /^\[!(\w+)\]\s*(.*)$/.exec(body[0] ?? '')
      if (callout) {
        out.push(block('callout', { props: { tone: TONES[callout[1].toUpperCase()] ?? 'note', ...(callout[2] ? { title: callout[2] } : {}) }, children: parseLines(body.slice(1), pageByTitle) }))
      } else {
        // The quote's first paragraph is its own text; what follows is inside it.
        const inside = parseLines(body, pageByTitle)
        const head = inside[0]?.type === 'paragraph' ? inside.shift() : undefined
        out.push(block('quote', { content: head?.content, children: inside }))
      }
      continue
    }
    // Tables: a header row, a rule, rows.
    if (/^\s*\|.*\|\s*$/.test(line) && /^\s*\|?\s*:?-{3,}/.test(lines[i + 1] ?? '')) {
      const cells = (row: string) => row.trim().replace(/^\||\|$/g, '').split(/(?<!\\)\|/).map((cell) => inline(cell.replace(/\\\|/g, '|')))
      const rows = [cells(line)]
      for (i += 2; i < lines.length && /^\s*\|/.test(lines[i]); i++) rows.push(cells(lines[i]))
      out.push(block('table', { props: { header: true, rows } }))
      continue
    }
    // Lists, nested by indentation.
    const item = /^(\s*)([-*+]|\d+[.)])\s+(\[([ xX])\]\s+)?(.*)$/.exec(line)
    if (item) {
      const depth = item[1].length
      const type = item[4] !== undefined ? 'todo' : /\d/.test(item[2]) ? 'numbered' : 'bulleted'
      // The lines indented deeper than the item are inside it (a blank line between them too).
      const nested: string[] = []
      for (i++; i < lines.length; i++) {
        const next = lines[i]
        if (!next.trim()) {
          if (indentOf(lines[i + 1] ?? '') > depth && (lines[i + 1] ?? '').trim()) nested.push('')
          else break
        } else if (indentOf(next) > depth) nested.push(next.slice(Math.min(depth + 2, indentOf(next))))
        else break
      }
      out.push(block(type, { props: type === 'todo' ? { checked: item[4].toLowerCase() === 'x' } : undefined, content: inline(item[5]), children: parseLines(nested, pageByTitle) }))
      continue
    }
    // An image alone on its line.
    const image = /^\s*!\[([^\]]*)\]\((https?:\/\/[^)\s]+)\)\s*$/.exec(line)
    if (image) {
      out.push(block('image', { props: { src: image[2] }, content: image[1] ? [{ text: image[1] }] : undefined }))
      i++
      continue
    }
    // A paragraph runs until a blank line or another block starts.
    const text: string[] = [line.trim()]
    for (i++; i < lines.length && lines[i].trim() && !/^\s*(#{1,6}\s|>|```|~~~|[-*+]\s|\d+[.)]\s|\|)/.test(lines[i]); i++) text.push(lines[i].trim())
    out.push(block('paragraph', { content: inline(text.join(' ')) }))
  }
  return out
}
