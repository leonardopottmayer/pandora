import { useState, type ReactNode } from 'react'
import { Button, Spin, Typography } from 'antd'
import { DownOutlined, RightOutlined } from '@ant-design/icons'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'

const INDENT = 24

/** One level of a folder tree, fetched when its parent opens — a disk's tree never loads at once. */
export function LazyLevel<T>({
  queryKey,
  load,
  depth,
  children,
}: {
  queryKey: readonly unknown[]
  load: () => Promise<T[]>
  depth: number
  children: (node: T) => ReactNode
}) {
  const { t } = useTranslation()
  const { data, isLoading, error } = useQuery({ queryKey, queryFn: load })

  if (isLoading) return <Spin size="small" style={{ marginLeft: depth * INDENT + 8 }} />
  if (error || data?.length === 0)
    return (
      <Typography.Text type="secondary" className="block py-0.5 text-xs" style={{ paddingLeft: depth * INDENT + 32 }}>
        {error ? error.message : t('files.tree.empty')}
      </Typography.Text>
    )
  return <>{data?.map(children)}</>
}

/** A row of the tree; `renderChildren` draws the next level once it is opened. */
export function TreeRow({
  depth,
  expandable,
  label,
  renderChildren,
  defaultOpen = false,
}: {
  depth: number
  expandable: boolean
  label: ReactNode
  renderChildren: () => ReactNode
  defaultOpen?: boolean
}) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(defaultOpen)
  return (
    <>
      <div className="flex min-h-8 items-center gap-1" style={{ paddingLeft: depth * INDENT }}>
        {expandable ? (
          <Button
            type="text"
            size="small"
            aria-label={open ? t('files.tree.collapse') : t('files.tree.expand')}
            icon={open ? <DownOutlined /> : <RightOutlined />}
            onClick={() => setOpen(!open)}
          />
        ) : (
          <span className="inline-block w-6" />
        )}
        {label}
      </div>
      {open && renderChildren()}
    </>
  )
}
