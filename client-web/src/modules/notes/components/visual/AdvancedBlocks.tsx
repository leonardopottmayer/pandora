import { useEffect, useId, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NodeViewContent, NodeViewWrapper, type NodeViewProps } from '@tiptap/react'
import DOMPurify from 'dompurify'
import { App, Button, Dropdown, Input, Select, Spin } from 'antd'
import { CopyOutlined, DeleteOutlined, EditOutlined, PlusOutlined, RightOutlined, SyncOutlined } from '@ant-design/icons'
import { usePage, usePageTree } from '../../hooks/usePages'
import { useSpaces } from '../../hooks/useSpaces'
import { useTags } from '../../hooks/useTags'
import type { Block, Inline } from '../../lib/blocks'
import { plainText } from '../../lib/blocks'
import { loadKatex, loadMermaid } from '../../lib/lazyRenderers'
import { useVisualEditorContext } from './visualContext'
import { VisualEditor } from './VisualEditor'

// The advanced blocks: diagrams and formulas, a synced block and its copies, a template button, a
// decision, and the blocks that list pages (breadcrumb, sub-pages, pages by tag) or describe this one
// (page properties).

type Props = Record<string, unknown>
type Update = (props: Props) => void

const isDark = () => document.documentElement.style.colorScheme === 'dark'

// ---------- Mermaid and math ----------

/** The code of a diagram or a formula, typed in a field under it while the block is selected. */
function SourceField({ value, label, onChange }: { value: string; label: string; onChange: (value: string) => void }) {
  return (
    <Input.TextArea
      className="notes-source-field"
      autoSize={{ minRows: 2, maxRows: 16 }}
      value={value}
      aria-label={label}
      placeholder={label}
      spellCheck={false}
      onChange={(event) => onChange(event.target.value)}
    />
  )
}

export function MermaidBlock({ props, editable, selected, update }: { props: Props; editable: boolean; selected: boolean; update: Update }) {
  const { t } = useTranslation()
  const code = String(props.code ?? '')
  const id = useId().replace(/[^a-zA-Z0-9]/g, '')
  const [drawn, setDrawn] = useState<{ svg: string | null; error: string | null }>({ svg: null, error: null })

  useEffect(() => {
    if (!code.trim()) return
    let current = true
    const timer = window.setTimeout(async () => {
      try {
        const mermaid = await loadMermaid()
        // Strict: no scripts, no clicks to run code; labels as SVG text, so nothing needs HTML inside.
        mermaid.initialize({ startOnLoad: false, securityLevel: 'strict', theme: isDark() ? 'dark' : 'default', flowchart: { htmlLabels: false } })
        const { svg } = await mermaid.render(`mermaid-${id}-${Date.now()}`, code)
        if (current) setDrawn({ svg: DOMPurify.sanitize(svg, { USE_PROFILES: { svg: true, svgFilters: true } }), error: null })
      } catch (error) {
        if (current) setDrawn({ svg: null, error: error instanceof Error ? error.message : String(error) })
      }
    }, 300)
    return () => {
      current = false
      window.clearTimeout(timer)
    }
  }, [code, id])

  return (
    <div className="notes-diagram" contentEditable={false}>
      {code.trim() && drawn.svg && <div className="notes-diagram-svg" dangerouslySetInnerHTML={{ __html: drawn.svg }} />}
      {code.trim() && drawn.error && <div className="notes-source-error">{t('notes.advanced.diagramError')}</div>}
      {!code.trim() && !editable && <div className="notes-rich-empty">{t('notes.advanced.emptyDiagram')}</div>}
      {editable && (selected || !code.trim()) && <SourceField value={code} label={t('notes.advanced.mermaidSource')} onChange={(value) => update({ code: value })} />}
    </div>
  )
}

export function MathBlock({ props, editable, selected, update }: { props: Props; editable: boolean; selected: boolean; update: Update }) {
  const { t } = useTranslation()
  const latex = String(props.latex ?? '')
  const [html, setHtml] = useState<string | null>(null)

  useEffect(() => {
    let current = true
    void loadKatex().then((katex) => {
      // KaTeX writes its own markup from the formula; `trust: false` keeps \href and the like out.
      const out = katex.renderToString(latex, { displayMode: true, throwOnError: false, trust: false, maxSize: 50, maxExpand: 1000 })
      if (current) setHtml(out)
    })
    return () => {
      current = false
    }
  }, [latex])

  return (
    <div className="notes-math" contentEditable={false}>
      {latex.trim() ? html ? <div dangerouslySetInnerHTML={{ __html: html }} /> : <Spin size="small" /> : !editable && <div className="notes-rich-empty">{t('notes.advanced.emptyMath')}</div>}
      {editable && (selected || !latex.trim()) && <SourceField value={latex} label={t('notes.advanced.mathSource')} onChange={(value) => update({ latex: value })} />}
    </div>
  )
}

// ---------- Synced blocks ----------

function findBlock(blocks: Block[], id: string): Block | null {
  for (const block of blocks) {
    if (block.id === id) return block
    const found = findBlock(block.children ?? [], id)
    if (found) return found
  }
  return null
}

/** The original: edited here; "copy to sync" puts it on the clipboard, to paste into other pages. */
export function SyncedView({ node }: NodeViewProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { pageId } = useVisualEditorContext()

  async function copy() {
    const blockId = String(node.attrs.blockId ?? '')
    const html = `<div data-synced-ref data-page="${pageId}" data-block="${blockId}"></div>`
    try {
      await navigator.clipboard.write([
        new ClipboardItem({ 'text/html': new Blob([html], { type: 'text/html' }), 'text/plain': new Blob([t('notes.advanced.syncedPlain')], { type: 'text/plain' }) }),
      ])
      message.success(t('notes.advanced.syncedCopied'))
    } catch {
      message.error(t('notes.blocks.copyError'))
    }
  }

  return (
    <NodeViewWrapper className="notes-synced">
      <div className="notes-synced-bar" contentEditable={false}>
        <span><SyncOutlined /> {t('notes.advanced.synced')}</span>
        <Button size="small" type="text" icon={<CopyOutlined />} onClick={() => void copy()}>{t('notes.advanced.copyToSync')}</Button>
      </div>
      <NodeViewContent className="notes-synced-body" />
    </NodeViewWrapper>
  )
}

/** A copy: the original's blocks as they are now, read-only; a click takes you to edit them there. */
export function SyncedCopy({ props }: { props: Props }) {
  const { t } = useTranslation()
  const { pagesById, onOpenPage } = useVisualEditorContext()
  const pageId = String(props.pageId ?? '')
  const blockId = String(props.blockId ?? '')
  const source = pagesById.get(pageId)
  const { data: page, isLoading } = usePage(source ? pageId : null)
  const original = page ? findBlock(page.content.blocks, blockId) : null

  return (
    <div className="notes-synced notes-synced-copy" contentEditable={false}>
      <div className="notes-synced-bar">
        <span><SyncOutlined /> {source ? t('notes.advanced.syncedFrom', { title: source.title || t('notes.untitled') }) : t('notes.advanced.syncedGone')}</span>
        {source && <Button size="small" type="text" icon={<EditOutlined />} onClick={() => onOpenPage(pageId)}>{t('notes.advanced.editOriginal')}</Button>}
      </div>
      {isLoading ? <Spin size="small" /> : original ? (
        <VisualEditor
          docId={`synced-${pageId}-${blockId}-${page?.updatedAt ?? ''}`}
          initialContent={original.children ?? []}
          pages={[...pagesById.values()]}
          editable={false}
          onOpenPage={onOpenPage}
        />
      ) : source && <div className="notes-rich-empty">{t('notes.advanced.syncedGone')}</div>}
    </div>
  )
}

// ---------- Template button ----------

export function TemplateButtonView({ node, editor, getPos, updateAttributes }: NodeViewProps) {
  const { t } = useTranslation()
  const label = String(node.attrs.label ?? '')
  const editing = !!node.attrs.editing
  const editable = editor.isEditable

  function insert() {
    const pos = getPos()
    if (typeof pos !== 'number') return
    // The copies get fresh ids from BlockIds; the template keeps its own.
    editor.chain().focus().insertContentAt(pos + node.nodeSize, node.content.toJSON()).run()
  }

  return (
    <NodeViewWrapper className="notes-template" data-editing={editing}>
      <div className="notes-template-bar" contentEditable={false}>
        <Button icon={<PlusOutlined />} disabled={!editable} onClick={insert}>{label || t('notes.advanced.templateDefault')}</Button>
        {editable && (
          <Button
            type="text"
            size="small"
            icon={<RightOutlined rotate={editing ? 90 : 0} />}
            aria-expanded={editing}
            onClick={() => updateAttributes({ editing: !editing })}
          >
            {t('notes.advanced.editTemplate')}
          </Button>
        )}
      </div>
      {editing && (
        <Input
          size="small"
          className="notes-template-label"
          value={label}
          placeholder={t('notes.advanced.templateLabel')}
          aria-label={t('notes.advanced.templateLabel')}
          onChange={(event) => updateAttributes({ label: event.target.value })}
        />
      )}
      <NodeViewContent className="notes-template-body" />
    </NodeViewWrapper>
  )
}

// ---------- Decision ----------

const DECISION_STATUSES = ['open', 'decided', 'rejected'] as const

export function DecisionView({ node, editor, updateAttributes }: NodeViewProps) {
  const { t } = useTranslation()
  const status = DECISION_STATUSES.includes(node.attrs.status) ? (node.attrs.status as (typeof DECISION_STATUSES)[number]) : 'open'
  const pill = <span className={`notes-decision-status notes-decision-${status}`}>{t(`notes.advanced.decision.${status}`)}</span>
  return (
    <NodeViewWrapper className="notes-decision" data-status={status}>
      <span contentEditable={false} className="notes-decision-pill">
        {editor.isEditable ? (
          <Dropdown
            trigger={['click']}
            menu={{
              selectedKeys: [status],
              items: DECISION_STATUSES.map((value) => ({ key: value, label: t(`notes.advanced.decision.${value}`), onClick: () => updateAttributes({ status: value }) })),
            }}
          >
            <button type="button" aria-label={t('notes.advanced.decision.change')}>{pill}</button>
          </Dropdown>
        ) : pill}
      </span>
      <NodeViewContent className="notes-decision-text" />
    </NodeViewWrapper>
  )
}

// ---------- Blocks that list pages ----------

function PageRow({ id }: { id: string }) {
  const { t } = useTranslation()
  const { pagesById, onOpenPage } = useVisualEditorContext()
  const page = pagesById.get(id)
  if (!page) return null
  return (
    <li>
      <button type="button" className="notes-page-row" onClick={() => onOpenPage(id)}>
        <span aria-hidden="true">{page.icon ?? '📄'}</span>
        <span className="notes-page-row-title">{page.title || t('notes.untitled')}</span>
      </button>
    </li>
  )
}

/** Where this page sits: its space, the folders and pages above it. */
export function BreadcrumbBlock() {
  const { t } = useTranslation()
  const { pagesById, foldersById, pageId, onOpenPage, onOpenFolder } = useVisualEditorContext()
  const { data: spaces } = useSpaces(true)
  const page = pagesById.get(pageId)
  const crumbs: { key: string; label: string; open?: () => void }[] = []
  let parent = page?.parentId ? pagesById.get(page.parentId) : undefined
  let top = page
  while (parent) {
    const above = parent
    crumbs.unshift({ key: above.id, label: `${above.icon ?? '📄'} ${above.title || t('notes.untitled')}`, open: () => onOpenPage(above.id) })
    top = above
    parent = above.parentId ? pagesById.get(above.parentId) : undefined
  }
  let folder = top?.folderId ? foldersById.get(top.folderId) : undefined
  while (folder) {
    const above = folder
    crumbs.unshift({ key: above.id, label: `${above.icon ?? '📁'} ${above.name}`, open: onOpenFolder && (() => onOpenFolder(above.id)) })
    folder = above.parentId ? foldersById.get(above.parentId) : undefined
  }
  const space = spaces?.find((candidate) => candidate.id === page?.spaceId)
  if (space) crumbs.unshift({ key: space.id, label: space.name })
  if (page) crumbs.push({ key: page.id, label: `${page.icon ?? '📄'} ${page.title || t('notes.untitled')}` })

  return (
    <nav className="notes-breadcrumb-block" aria-label={t('notes.breadcrumb')} contentEditable={false}>
      {crumbs.map((crumb, index) => (
        <span key={crumb.key} className="notes-breadcrumb-crumb">
          {index > 0 && <span aria-hidden="true" className="notes-breadcrumb-sep">/</span>}
          {crumb.open ? <button type="button" onClick={crumb.open}>{crumb.label}</button> : <span>{crumb.label}</span>}
        </span>
      ))}
    </nav>
  )
}

export function SubpagesBlock() {
  const { t } = useTranslation()
  const { pagesById, pageId } = useVisualEditorContext()
  const children = [...pagesById.values()].filter((page) => page.parentId === pageId && !page.isArchived).sort((a, b) => a.orderIndex - b.orderIndex)
  return (
    <div className="notes-page-list" contentEditable={false}>
      {children.length ? <ul>{children.map((page) => <PageRow key={page.id} id={page.id} />)}</ul> : <div className="notes-rich-empty">{t('notes.advanced.noSubpages')}</div>}
    </div>
  )
}

export function TagPagesBlock({ props, editable, update }: { props: Props; editable: boolean; update: Update }) {
  const { t } = useTranslation()
  const { pageId } = useVisualEditorContext()
  const { tags } = useTags()
  const names = ((props.tags as string[] | undefined) ?? []).map(String)
  const ids = tags.filter((tag) => names.some((name) => name.toLowerCase() === tag.name.toLowerCase())).map((tag) => tag.id)
  const { data: pages, isLoading } = usePageTree(false, ids)
  const found = ids.length ? (pages ?? []).filter((page) => page.id !== pageId) : []

  return (
    <div className="notes-page-list" contentEditable={false}>
      {editable && (
        <Select
          mode="multiple"
          size="small"
          className="notes-tag-pages-select"
          value={names}
          placeholder={t('notes.advanced.pickTags')}
          aria-label={t('notes.advanced.pickTags')}
          options={tags.map((tag) => ({ value: tag.name, label: `#${tag.name}` }))}
          onChange={(value: string[]) => update({ tags: value })}
        />
      )}
      {!editable && names.length > 0 && <div className="notes-rich-caption">{names.map((name) => `#${name}`).join(' ')}</div>}
      {isLoading && ids.length > 0 ? <Spin size="small" /> : found.length ? (
        <ul>{found.map((page) => <PageRow key={page.id} id={page.id} />)}</ul>
      ) : <div className="notes-rich-empty">{t(names.length ? 'notes.advanced.noTaggedPages' : 'notes.advanced.pickTags')}</div>}
    </div>
  )
}

// ---------- Page properties ----------

interface PropertyRow {
  key?: Inline[]
  value?: Inline[]
}

/** Facts about the page as a two-column table: owner, status, due date… Searched like the text. */
export function PropertiesBlock({ props, editable, update }: { props: Props; editable: boolean; update: Update }) {
  const { t } = useTranslation()
  const rows = (props.rows as PropertyRow[] | undefined) ?? []
  const cell = (text: string): Inline[] => (text ? [{ text }] : [])
  const set = (index: number, field: 'key' | 'value', text: string) =>
    update({ rows: rows.map((row, other) => (other === index ? { ...row, [field]: cell(text) } : row)) })

  return (
    <div className="notes-properties" contentEditable={false}>
      <table>
        <tbody>
          {rows.map((row, index) => (
            <tr key={index}>
              <th scope="row">
                {editable
                  ? <Input variant="borderless" size="small" value={plainText(row.key)} placeholder={t('notes.advanced.propertyKey')} aria-label={t('notes.advanced.propertyKey')} onChange={(event) => set(index, 'key', event.target.value)} />
                  : plainText(row.key)}
              </th>
              <td>
                {editable
                  ? <Input variant="borderless" size="small" value={plainText(row.value)} placeholder={t('notes.advanced.propertyValue')} aria-label={t('notes.advanced.propertyValue')} onChange={(event) => set(index, 'value', event.target.value)} />
                  : plainText(row.value)}
              </td>
              {editable && (
                <td className="notes-properties-tools">
                  <Button type="text" size="small" icon={<DeleteOutlined />} aria-label={t('notes.advanced.removeProperty')} onClick={() => update({ rows: rows.filter((_, other) => other !== index) })} />
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
      {editable && (
        <Button type="text" size="small" icon={<PlusOutlined />} onClick={() => update({ rows: [...rows, { key: [], value: [] }] })}>
          {t('notes.advanced.addProperty')}
        </Button>
      )}
      {!editable && rows.length === 0 && <div className="notes-rich-empty">{t('notes.advanced.noProperties')}</div>}
    </div>
  )
}
