import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Typography, theme } from 'antd'
import { FileTextOutlined, FolderOutlined, RightOutlined } from '@ant-design/icons'
import { useNodeText } from '../hooks/useNodeText'
import type { SpaceNode } from '../lib/spaceTree'

// The folders and pages of a space as the overview and a folder page show them: rows, cards or a tree.

export function NodeIcon({ node, size = 16 }: { node: SpaceNode; size?: number }) {
  const icon = node.kind === 'folder' ? node.folder.icon : node.page.icon
  return (
    <span aria-hidden className="inline-flex shrink-0 items-center justify-center" style={{ fontSize: size, width: size + 4 }}>
      {icon ?? (node.kind === 'folder' ? <FolderOutlined /> : <FileTextOutlined />)}
    </span>
  )
}

const panel = (token: ReturnType<typeof theme.useToken>['token']) => ({
  border: `1px solid ${token.colorBorderSecondary}`,
  background: token.colorBgContainer,
})

/** One row per node: name, a secondary line, the date on the right. */
export function ItemList({ nodes, onOpen }: { nodes: SpaceNode[]; onOpen: (node: SpaceNode) => void }) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const text = useNodeText()
  return (
    <div className="overflow-hidden rounded-xl" style={panel(token)}>
      {nodes.map((node, index) => (
        <button
          key={node.id}
          type="button"
          className="flex w-full cursor-pointer items-center gap-3 px-4 py-2.5 text-left hover:bg-[var(--ant-color-fill-quaternary)]"
          // The shorthand first: set after, `border` would wipe the top line.
          style={{ border: 0, background: 'transparent', borderTop: index ? `1px solid ${token.colorBorderSecondary}` : undefined }}
          onClick={() => onOpen(node)}
        >
          <NodeIcon node={node} size={18} />
          <span className="flex min-w-0 flex-1 flex-col">
            <Typography.Text className="truncate font-medium">{text.name(node)}</Typography.Text>
            <Typography.Text type="secondary" className="truncate text-xs">
              {[node.kind === 'folder' ? t('notes.folders.kind') : t('notes.counts.page'), text.counts(node)].filter(Boolean).join(' · ')}
            </Typography.Text>
          </span>
          <Typography.Text type="secondary" className="hidden shrink-0 text-xs tabular-nums sm:inline">
            {text.date(node)}
          </Typography.Text>
        </button>
      ))}
    </div>
  )
}

/** A card per node: a folder shows what it holds, a page its sub-pages. */
export function ItemCards({ nodes, onOpen }: { nodes: SpaceNode[]; onOpen: (node: SpaceNode) => void }) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const text = useNodeText()
  const shown = 5
  return (
    <div className="grid grid-cols-1 items-start gap-3 sm:grid-cols-2 xl:grid-cols-3">
      {nodes.map((node) => (
        <div
          key={node.id}
          role="button"
          tabIndex={0}
          className="flex cursor-pointer flex-col gap-2.5 rounded-xl p-3.5 transition-shadow hover:shadow-sm"
          style={panel(token)}
          onClick={() => onOpen(node)}
          onKeyDown={(event) => event.key === 'Enter' && onOpen(node)}
        >
          <div className="flex min-w-0 items-center gap-2.5">
            <span className="flex size-9 shrink-0 items-center justify-center rounded-lg" style={{ background: token.colorFillTertiary }}>
              <NodeIcon node={node} size={18} />
            </span>
            <span className="flex min-w-0 flex-col">
              <Typography.Text strong className="truncate">{text.name(node)}</Typography.Text>
              <Typography.Text type="secondary" className="truncate text-xs">
                {text.counts(node) || (node.kind === 'folder' ? t('notes.folders.kind') : t('notes.counts.page'))}
              </Typography.Text>
            </span>
          </div>
          {node.children.length > 0 && (
            <ul className="m-0 flex list-none flex-col gap-0.5 p-0 pt-2" style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}>
              {node.children.slice(0, shown).map((child) => (
                <li key={child.id}>
                  <button
                    type="button"
                    className="flex w-full cursor-pointer items-center gap-2 rounded px-1.5 py-0.5 text-left hover:bg-[var(--ant-color-fill-tertiary)]"
                    style={{ background: 'transparent', border: 0 }}
                    onClick={(event) => {
                      event.stopPropagation()
                      onOpen(child)
                    }}
                  >
                    <NodeIcon node={child} />
                    <Typography.Text className="flex-1 truncate">{text.name(child)}</Typography.Text>
                  </button>
                </li>
              ))}
              {node.children.length > shown && (
                <Typography.Text type="secondary" className="px-1.5 text-xs">
                  {t('notes.counts.more', { count: node.children.length - shown })}
                </Typography.Text>
              )}
            </ul>
          )}
          <Typography.Text type="secondary" className="text-xs">{t('notes.edited', { when: text.edited(node) })}</Typography.Text>
        </div>
      ))}
    </div>
  )
}

/** The whole hierarchy as rows that expand and collapse. */
export function ItemTree({ nodes, onOpen }: { nodes: SpaceNode[]; onOpen: (node: SpaceNode) => void }) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const text = useNodeText()
  const [closed, setClosed] = useState<Set<string>>(new Set())
  const rows: { node: SpaceNode; depth: number }[] = []
  const add = (list: SpaceNode[], depth: number) =>
    list.forEach((node) => {
      rows.push({ node, depth })
      if (!closed.has(node.id)) add(node.children, depth + 1)
    })
  add(nodes, 0)

  const toggle = (id: string) =>
    setClosed((current) => {
      const next = new Set(current)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  return (
    <div className="overflow-hidden rounded-xl" style={panel(token)}>
      {rows.map(({ node, depth }, index) => (
        <div
          key={node.id}
          className="flex items-center gap-2 py-2 pe-4 hover:bg-[var(--ant-color-fill-quaternary)]"
          style={{ paddingInlineStart: 8 + depth * 22, borderTop: index ? `1px solid ${token.colorBorderSecondary}` : undefined }}
        >
          <button
            type="button"
            aria-label={t(closed.has(node.id) ? 'notes.visual.expandBlock' : 'notes.visual.collapseBlock')}
            aria-expanded={!closed.has(node.id)}
            className="flex size-6 shrink-0 cursor-pointer items-center justify-center rounded"
            style={{ background: 'transparent', border: 0, visibility: node.children.length ? 'visible' : 'hidden' }}
            onClick={() => toggle(node.id)}
          >
            <RightOutlined className="text-[10px] transition-transform" style={{ transform: closed.has(node.id) ? undefined : 'rotate(90deg)' }} />
          </button>
          <button
            type="button"
            className="flex min-w-0 flex-1 cursor-pointer items-center gap-2 text-left"
            style={{ background: 'transparent', border: 0, padding: 0 }}
            onClick={() => onOpen(node)}
          >
            <NodeIcon node={node} />
            <Typography.Text className="truncate">{text.name(node)}</Typography.Text>
            <Typography.Text type="secondary" className="hidden truncate text-xs md:inline">{text.counts(node)}</Typography.Text>
          </button>
          <Typography.Text type="secondary" className="hidden shrink-0 text-xs tabular-nums sm:inline">{text.date(node)}</Typography.Text>
        </div>
      ))}
    </div>
  )
}
