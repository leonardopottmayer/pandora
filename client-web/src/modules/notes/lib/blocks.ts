import type { JSONContent } from '@tiptap/core'

// A page is a document of blocks (see docs/modules/notes/en/product-plan.md). The editor works on
// TipTap's own JSON; this file converts between the two, so what is stored never depends on the
// editor. Every block keeps its id across edits: the editor carries it as the `blockId` attribute.

export interface Mark {
  type: string
  attrs?: Record<string, unknown>
}

export interface TextInline {
  text: string
  marks?: Mark[]
}

export interface MentionInline {
  mention: { page: string }
}

/** A date, with the Agenda reminder made for it: `2026-12-01`, or `2026-12-01T14:00` with a time. */
export interface DateChip {
  value: string
  reminderId?: string
}

/** A status pill typed in the text. */
export interface StatusChip {
  label: string
  color?: string
}

/** An item of another module (a task, a reminder, an account, a card), named as it was when linked. */
export interface ItemChip {
  module: string
  kind: string
  id: string
  label: string
}

/** A formula in the line of text. */
export interface MathChip {
  latex: string
}

export interface ChipData {
  date: DateChip
  status: StatusChip
  item: ItemChip
  math: MathChip
}

export type ChipKind = keyof ChipData
export const CHIP_KINDS: ChipKind[] = ['date', 'status', 'item', 'math']

export type ChipInline = { [K in ChipKind]: { [P in K]: ChipData[K] } }[ChipKind]

export type Inline = TextInline | MentionInline | ChipInline

export interface Block {
  id: string
  type: string
  props?: Record<string, unknown>
  content?: Inline[]
  children?: Block[]
}

export interface BlockDocument {
  version: 1
  blocks: Block[]
}

export const emptyDocument = (): BlockDocument => ({ version: 1, blocks: [] })

export function newBlockId(): string {
  return crypto.randomUUID().replace(/-/g, '').slice(0, 12)
}

const isMention = (inline: Inline): inline is MentionInline => 'mention' in inline
const isText = (inline: Inline): inline is TextInline => 'text' in inline

/** The kind and data of a chip, or null for text and mentions. */
export function chipOf(inline: Inline): { kind: ChipKind; data: ChipData[ChipKind] } | null {
  const kind = CHIP_KINDS.find((candidate) => candidate in inline)
  return kind ? { kind, data: (inline as Record<string, unknown>)[kind] as ChipData[ChipKind] } : null
}

/** A chip as words: what search and a version diff read. */
export function chipText(kind: ChipKind, data: ChipData[ChipKind]): string {
  if (kind === 'date') return (data as DateChip).value
  if (kind === 'status') return (data as StatusChip).label
  if (kind === 'item') return (data as ItemChip).label
  return (data as MathChip).latex
}

/** The text of an inline list; a mention reads as the title `titleOf` gives it, or nothing. */
export function plainText(content: Inline[] = [], titleOf?: (pageId: string) => string | undefined): string {
  return content
    .map((inline) => {
      if (isText(inline)) return inline.text
      if (isMention(inline)) return titleOf?.(inline.mention.page) ?? ''
      const chip = chipOf(inline)
      return chip ? chipText(chip.kind, chip.data) : ''
    })
    .join('')
}

// ---------- Tags: one block at the top of the page, edited in the header ----------

export function tagsOf(blocks: Block[]): string[] {
  const tags = blocks.find((block) => block.type === 'tags')?.props?.tags
  return Array.isArray(tags) ? tags.map(String) : []
}

/** The blocks with the tags block set to `tags` (kept first, keeping its id), or removed when there are none. */
export function withTags(blocks: Block[], tags: string[]): Block[] {
  const current = blocks.find((block) => block.type === 'tags')
  const rest = blocks.filter((block) => block.type !== 'tags')
  return tags.length ? [{ id: current?.id ?? newBlockId(), type: 'tags', props: { tags } }, ...rest] : rest
}

// ---------- Editor → blocks ----------

/** Only the attributes a mark means something by: a link's address, a colour, a comment's thread. */
function markOf(mark: { type: string; attrs?: Record<string, unknown> }): Mark {
  if (mark.type === 'link') return { type: 'link', attrs: { href: mark.attrs?.href ?? '' } }
  if (mark.type === 'color' || mark.type === 'highlight') return { type: mark.type, attrs: { color: mark.attrs?.color ?? null } }
  if (mark.type === 'comment') return { type: 'comment', attrs: { thread: mark.attrs?.thread ?? null } }
  return { type: mark.type }
}

function inlinesFrom(nodes: JSONContent[] = []): Inline[] {
  const out: Inline[] = []
  // Text next to text with the same marks is one run, whatever the editor split it into.
  const pushText = (text: string, marks?: Mark[]) => {
    const last = out[out.length - 1]
    if (last && isText(last) && JSON.stringify(last.marks ?? []) === JSON.stringify(marks ?? [])) last.text += text
    else out.push(marks?.length ? { text, marks } : { text })
  }
  for (const node of nodes) {
    if (node.type === 'text' && node.text) {
      pushText(node.text, node.marks?.map(markOf))
    } else if (node.type === 'hardBreak') {
      pushText('\n')
    } else if (node.type === 'mention' && node.attrs?.pageId) {
      out.push({ mention: { page: String(node.attrs.pageId) } })
    } else if (node.type === 'chip' && CHIP_KINDS.includes(node.attrs?.kind) && node.attrs?.data) {
      out.push({ [node.attrs.kind]: node.attrs.data } as ChipInline)
    }
  }
  return out
}

function block(id: string, type: string, props?: Record<string, unknown>, content?: Inline[], children?: Block[]): Block {
  const result: Block = { id, type }
  const kept = props && Object.fromEntries(Object.entries(props).filter(([, value]) => value !== undefined && value !== null && value !== '' && value !== false))
  if (kept && Object.keys(kept).length) result.props = kept
  if (content?.length) result.content = content
  if (children?.length) result.children = children
  return result
}

const idOf = (node: JSONContent) => String(node.attrs?.blockId || newBlockId())

const STYLE_KEYS = ['color', 'background', 'align'] as const

/** A block's colour, background and alignment, as props; empty ones are dropped by `block`. */
const styleOf = (attrs: Record<string, unknown> = {}) => Object.fromEntries(STYLE_KEYS.map((key) => [key, attrs[key]]))

/** The same, back as the editor's attributes. */
const styleAttrs = (props: Record<string, unknown> = {}) =>
  Object.fromEntries(STYLE_KEYS.filter((key) => props[key]).map((key) => [key, props[key]]))

/** A container whose first paragraph is its own text (a list item, a quote); the rest are its children. */
function headAndRest(node: JSONContent): [Inline[], Block[]] {
  const [first, ...rest] = node.content ?? []
  return first?.type === 'paragraph' ? [inlinesFrom(first.content), toBlocks(rest)] : [[], toBlocks(node.content)]
}

function nodeToBlocks(node: JSONContent): Block[] {
  const attrs = node.attrs ?? {}
  switch (node.type) {
    case 'paragraph':
      return [block(idOf(node), 'paragraph', styleOf(attrs), inlinesFrom(node.content))]
    case 'heading':
      return [block(idOf(node), 'heading', { level: attrs.level ?? 1, ...styleOf(attrs) }, inlinesFrom(node.content))]
    case 'bulletList':
    case 'orderedList':
    case 'taskList': {
      const type = node.type === 'bulletList' ? 'bulleted' : node.type === 'orderedList' ? 'numbered' : 'todo'
      return (node.content ?? []).map((item) => {
        const [content, children] = headAndRest(item)
        return block(idOf(item), type, { ...(type === 'todo' ? { checked: !!item.attrs?.checked } : {}), ...styleOf(item.attrs) }, content, children)
      })
    }
    // Items picked out of their list (a selection of some of them) keep their kind.
    case 'listItem':
      return nodeToBlocks({ type: 'bulletList', content: [node] })
    case 'taskItem':
      return nodeToBlocks({ type: 'taskList', content: [node] })
    case 'blockquote': {
      const [content, children] = headAndRest(node)
      return [block(idOf(node), 'quote', styleOf(attrs), content, children)]
    }
    case 'codeBlock': {
      const text = (node.content ?? []).map((child) => child.text ?? '').join('')
      return [block(idOf(node), 'code', { language: attrs.language, ...styleOf(attrs) }, text ? [{ text }] : undefined)]
    }
    case 'horizontalRule':
      return [block(idOf(node), 'divider')]
    case 'callout':
      return [block(idOf(node), 'callout', { tone: attrs.calloutType, title: attrs.title, fold: attrs.fold, ...styleOf(attrs) }, undefined, toBlocks(node.content))]
    case 'details': {
      const title: Inline[] | undefined = attrs.title ? [{ text: String(attrs.title) }] : undefined
      // A toggle with a level is a collapsible heading: a heading whose section folds under it.
      return attrs.level
        ? [block(idOf(node), 'heading', { level: attrs.level, toggle: true, open: !!attrs.open, ...styleOf(attrs) }, title, toBlocks(node.content))]
        : [block(idOf(node), 'toggle', { open: !!attrs.open, ...styleOf(attrs) }, title, toBlocks(node.content))]
    }
    case 'columns':
      return [block(idOf(node), 'columns', undefined, undefined, (node.content ?? []).map((column) => block(idOf(column), 'column', undefined, undefined, toBlocks(column.content))))]
    case 'richBlock':
      return [block(idOf(node), String(attrs.kind), attrs.props as Record<string, unknown>)]
    case 'synced':
      return [block(idOf(node), 'synced', undefined, undefined, toBlocks(node.content))]
    case 'templateButton':
      return [block(idOf(node), 'template-button', { label: attrs.label }, undefined, toBlocks(node.content))]
    case 'decision':
      return [block(idOf(node), 'decision', { status: attrs.status === 'open' ? undefined : attrs.status }, inlinesFrom(node.content))]
    case 'pageLink':
      return attrs.pageId ? [block(idOf(node), 'page-link', { pageId: attrs.pageId, look: attrs.look === 'preview' ? 'preview' : undefined })] : []
    case 'table': {
      const rows = (node.content ?? []).map((row) =>
        (row.content ?? []).map((cell) =>
          (cell.content ?? []).flatMap((paragraph, index) => [...(index ? [{ text: '\n' }] : []), ...inlinesFrom(paragraph.content)]),
        ),
      )
      const header = node.content?.[0]?.content?.[0]?.type === 'tableHeader'
      return [block(idOf(node), 'table', { header, rows })]
    }
    case 'image':
      return [
        block(idOf(node), 'image', { mediaId: attrs.mediaId, src: attrs.src, width: attrs.width, align: attrs.align },
          attrs.caption ? [{ text: String(attrs.caption) }] : undefined),
      ]
    case 'mediaFile':
      return [block(idOf(node), 'file', { mediaId: attrs.mediaId, name: attrs.name })]
    case 'unknownBlock':
      return attrs.block ? [attrs.block as Block] : []
    default:
      return []
  }
}

/** The editor's document (its top-level content) as blocks. */
export function toBlocks(nodes: JSONContent[] = []): Block[] {
  return nodes.flatMap(nodeToBlocks)
}

// ---------- Blocks → editor ----------

function inlinesTo(content: Inline[] = []): JSONContent[] {
  const out: JSONContent[] = []
  for (const inline of content) {
    if (isMention(inline)) {
      out.push({ type: 'mention', attrs: { pageId: inline.mention.page } })
      continue
    }
    const chip = chipOf(inline)
    if (chip) {
      out.push({ type: 'chip', attrs: { kind: chip.kind, data: chip.data } })
      continue
    }
    if (!isText(inline)) continue
    inline.text.split('\n').forEach((part, index) => {
      if (index) out.push({ type: 'hardBreak' })
      if (!part) return
      out.push(
        inline.marks?.length
          ? { type: 'text', text: part, marks: inline.marks.map((mark) => (mark.attrs ? { type: mark.type, attrs: mark.attrs } : { type: mark.type })) }
          : { type: 'text', text: part },
      )
    })
  }
  return out
}

function paragraph(content?: Inline[], blockId?: string, props?: Record<string, unknown>): JSONContent {
  const nodes = inlinesTo(content)
  const attrs = { ...(blockId ? { blockId } : {}), ...styleAttrs(props) }
  return { type: 'paragraph', ...(Object.keys(attrs).length ? { attrs } : {}), ...(nodes.length ? { content: nodes } : {}) }
}

/** A container's body; ProseMirror wants at least one block in it. */
const body = (children: Block[] = []) => {
  const nodes = blocksToNodes(children)
  return nodes.length ? nodes : [paragraph()]
}

function blockToNode(b: Block): JSONContent {
  const props = b.props ?? {}
  const blockId = b.id
  const style = styleAttrs(props)
  switch (b.type) {
    case 'paragraph':
      return paragraph(b.content, blockId, props)
    case 'heading': {
      if (props.toggle) {
        return { type: 'details', attrs: { blockId, level: props.level ?? 1, open: !!props.open, title: plainText(b.content), ...style }, content: body(b.children) }
      }
      const nodes = inlinesTo(b.content)
      return { type: 'heading', attrs: { blockId, level: props.level ?? 1, ...style }, ...(nodes.length ? { content: nodes } : {}) }
    }
    case 'quote':
      return { type: 'blockquote', attrs: { blockId, ...style }, content: [paragraph(b.content), ...blocksToNodes(b.children ?? [])] }
    case 'code': {
      const text = plainText(b.content)
      return { type: 'codeBlock', attrs: { blockId, language: props.language ?? null, ...style }, ...(text ? { content: [{ type: 'text', text }] } : {}) }
    }
    case 'divider':
      return { type: 'horizontalRule', attrs: { blockId } }
    case 'callout':
      return { type: 'callout', attrs: { blockId, calloutType: props.tone ?? 'note', title: props.title ?? '', fold: props.fold ?? null, ...style }, content: body(b.children) }
    case 'toggle':
      return { type: 'details', attrs: { blockId, open: !!props.open, title: plainText(b.content), ...style }, content: body(b.children) }
    case 'columns': {
      const columns = (b.children ?? []).filter((child) => child.type === 'column').slice(0, 4)
      // The editor wants two at least: a lone column gets an empty neighbour.
      while (columns.length < 2) columns.push({ id: newBlockId(), type: 'column' })
      return { type: 'columns', attrs: { blockId }, content: columns.map((column) => ({ type: 'column', attrs: { blockId: column.id }, content: body(column.children) })) }
    }
    case 'bookmark':
    case 'embed':
    case 'video':
    case 'audio':
    case 'pdf':
    case 'gallery':
    case 'mermaid':
    case 'math':
    case 'synced-ref':
    case 'breadcrumb':
    case 'subpages':
    case 'tag-pages':
    case 'properties':
    case 'agenda-list':
    case 'finance-summary':
      case 'ai':
      return { type: 'richBlock', attrs: { blockId, kind: b.type, props } }
    case 'synced':
      return { type: 'synced', attrs: { blockId }, content: body(b.children) }
    case 'template-button':
      return { type: 'templateButton', attrs: { blockId, label: props.label ?? '' }, content: body(b.children) }
    case 'decision': {
      const nodes = inlinesTo(b.content)
      return { type: 'decision', attrs: { blockId, status: props.status ?? 'open' }, ...(nodes.length ? { content: nodes } : {}) }
    }
    case 'page-link':
      return { type: 'pageLink', attrs: { blockId, pageId: props.pageId ?? '', look: props.look === 'preview' ? 'preview' : 'card' } }
    case 'table': {
      const rows = (props.rows as Inline[][][] | undefined) ?? [[[]]]
      return {
        type: 'table',
        attrs: { blockId },
        content: rows.map((row, index) => ({
          type: 'tableRow',
          content: row.map((cell) => ({ type: props.header && index === 0 ? 'tableHeader' : 'tableCell', content: [paragraph(cell)] })),
        })),
      }
    }
    case 'image':
      return {
        type: 'image',
        attrs: { blockId, mediaId: props.mediaId ?? null, src: props.src ?? null, width: props.width ?? null, align: props.align ?? null, caption: plainText(b.content) },
      }
    case 'file':
      return { type: 'mediaFile', attrs: { blockId, mediaId: props.mediaId ?? null, name: props.name ?? '' } }
    default:
      // A type this editor does not know yet travels untouched.
      return { type: 'unknownBlock', attrs: { blockId, block: b } }
  }
}

const LIST_NODE: Record<string, string> = { bulleted: 'bulletList', numbered: 'orderedList', todo: 'taskList' }

function blocksToNodes(blocks: Block[]): JSONContent[] {
  const out: JSONContent[] = []
  let list: JSONContent | null = null
  for (const b of blocks) {
    const listType = LIST_NODE[b.type]
    if (!listType) {
      list = null
      if (b.type !== 'tags') out.push(blockToNode(b))
      continue
    }
    // Consecutive items of one kind are one list in the editor.
    if (!list || list.type !== listType) {
      list = { type: listType, content: [] }
      out.push(list)
    }
    const todo = listType === 'taskList'
    list.content!.push({
      type: todo ? 'taskItem' : 'listItem',
      attrs: { blockId: b.id, ...(todo ? { checked: !!b.props?.checked } : {}), ...styleAttrs(b.props) },
      content: [paragraph(b.content), ...blocksToNodes(b.children ?? [])],
    })
  }
  return out
}

/** Blocks as the editor's document. The tags block is left out: the header edits it. */
export function toEditorDoc(blocks: Block[]): JSONContent {
  const content = blocksToNodes(blocks)
  return { type: 'doc', content: content.length ? content : [paragraph()] }
}

// ---------- Text, for comparing versions ----------

/** One line per block, indented by depth, lists and headings marked: what a version diff compares. */
export function documentLines(blocks: Block[], titleOf: (pageId: string) => string | undefined): string[] {
  const lines: string[] = []
  const walk = (list: Block[], depth: number) => {
    let number = 0
    for (const b of list) {
      number = b.type === 'numbered' ? number + 1 : 0
      const text = plainText(b.content, (id) => `@${titleOf(id) ?? '?'}`)
      const indent = '  '.repeat(depth)
      const props = b.props ?? {}
      const line =
        b.type === 'heading' ? `${'#'.repeat(Number(props.level ?? 1))} ${text}`
          : b.type === 'bulleted' ? `${indent}- ${text}`
          : b.type === 'numbered' ? `${indent}${number}. ${text}`
          : b.type === 'todo' ? `${indent}- [${props.checked ? 'x' : ' '}] ${text}`
          : b.type === 'quote' ? `> ${text}`
          : b.type === 'divider' ? '---'
          : b.type === 'tags' ? (tagsOf([b]).map((tag) => `#${tag}`).join(' '))
          : b.type === 'image' ? `[image] ${text}`
          : b.type === 'file' ? `[file] ${String(props.name ?? '')}`
          : ['bookmark', 'embed', 'video', 'audio', 'pdf'].includes(b.type) ? `[${b.type}] ${String(props.title ?? props.url ?? props.name ?? '')}`
          : b.type === 'gallery' ? `[gallery] ${((props.items as unknown[] | undefined) ?? []).length}`
          : b.type === 'mermaid' ? `[mermaid]\n${String(props.code ?? '')}`
          : b.type === 'math' ? `[math] ${String(props.latex ?? '')}`
          : b.type === 'decision' ? `[${String(props.status ?? 'open')}] ${text}`
          : b.type === 'template-button' ? `[button] ${String(props.label ?? '')}`
          : b.type === 'properties' ? ((props.rows as { key?: Inline[]; value?: Inline[] }[] | undefined) ?? []).map((row) => `${plainText(row.key)}: ${plainText(row.value)}`).join('\n')
          : b.type === 'ai' ? `[ai] ${String(props.prompt ?? '')}\n${plainText(props.output as Inline[] | undefined)}`
          : ['synced', 'synced-ref', 'breadcrumb', 'subpages', 'tag-pages', 'agenda-list', 'finance-summary'].includes(b.type) ? `[${b.type}]`
          : b.type === 'page-link' ? `[page] ${titleOf(String(props.pageId)) ?? '?'}`
          : b.type === 'columns' || b.type === 'column' ? null
          : b.type === 'table' ? ((props.rows as Inline[][][] | undefined) ?? []).map((row) => row.map((cell) => plainText(cell)).join(' | ')).join('\n')
          : `${indent}${text}`
      if (line !== null) lines.push(...line.split('\n'))
      walk(b.children ?? [], depth + 1)
    }
  }
  walk(blocks, 0)
  return lines
}

// ---------- Headings, for the table of contents ----------

export interface Heading {
  id: string
  level: number
  text: string
}

/** Headings 1 to 3, in reading order, at any depth (inside a toggle or a callout too). */
export function headingsOf(blocks: Block[]): Heading[] {
  const out: Heading[] = []
  const walk = (list: Block[]) =>
    list.forEach((block) => {
      const text = plainText(block.content).trim()
      if (block.type === 'heading' && text) out.push({ id: block.id, level: Number(block.props?.level ?? 1), text })
      walk(block.children ?? [])
    })
  walk(blocks)
  return out
}
