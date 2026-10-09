import { useState } from 'react'
import { App, Breadcrumb, Button, Empty, Input, Select, Spin, Table, Tag, Tooltip, Typography } from 'antd'
import { FileOutlined, FolderFilled, FolderOpenOutlined } from '@ant-design/icons'
import { Link } from 'react-router-dom'
import { useInfiniteQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import { FilesGate } from '../components/FilesGate'
import { filesKeys, useFilesAgent, useRoots } from '../hooks/useFiles'
import { formatBytes, localPathOf } from '../lib/format'
import * as filesService from '../services/files.service'
import type { Entry, EntryStatus, FileCategory, Page, Root } from '../models'

const CATEGORIES: FileCategory[] = ['video', 'audio', 'image', 'document', 'ebook', 'archive', 'code', 'other']
const STATUSES: (EntryStatus | 'all')[] = ['present', 'missing', 'excluded', 'all']

/** The catalog from any device: browse a root folder by folder, or search every root by name fragments. */
export function CatalogPage() {
  return (
    <FilesGate>
      <Catalog />
    </FilesGate>
  )
}

function Catalog() {
  const { t } = useTranslation()
  const roots = useRoots()
  const [text, setText] = useState('')
  const [q, setQ] = useState('')
  const [category, setCategory] = useState<FileCategory | undefined>()
  const [status, setStatus] = useState<EntryStatus | 'all'>('present')
  const [rootId, setRootId] = useState<string | undefined>()
  const [path, setPath] = useState('/')

  if (roots.isLoading) return <Spin className="block py-16 text-center" />
  const allRoots = roots.data ?? []
  if (allRoots.length === 0)
    return (
      <Empty description={t('files.catalog.noRoots')}>
        <Link to="/files/roots">
          <Button type="primary">{t('files.catalog.noRootsAction')}</Button>
        </Link>
      </Empty>
    )

  const searching = q.trim() !== '' || category !== undefined || status !== 'present'
  const browsed = allRoots.find((r) => r.id === rootId) ?? allRoots[0]

  function openFolder(entry: Entry) {
    setText('')
    setQ('')
    setCategory(undefined)
    setStatus('present')
    setRootId(entry.rootId)
    setPath(entry.relativePath)
  }

  return (
    <div className="mx-auto flex max-w-6xl flex-col gap-4">
      <PageHeading title={t('files.catalog.title')} description={t('files.catalog.intro')} />

      <div className="flex flex-wrap gap-2">
        <Input.Search
          className="min-w-64 flex-1"
          allowClear
          placeholder={t('files.catalog.searchPlaceholder')}
          value={text}
          onChange={(e) => setText(e.target.value)}
          onSearch={setQ}
        />
        <Select
          className="w-44"
          value={rootId ?? (searching ? undefined : browsed.id)}
          placeholder={t('files.catalog.allRoots')}
          allowClear={searching}
          onChange={(id) => {
            setRootId(id)
            setPath('/')
          }}
          options={allRoots.map((r) => ({ value: r.id, label: r.name }))}
        />
        <Select
          className="w-40"
          value={category}
          placeholder={t('files.catalog.anyCategory')}
          allowClear
          onChange={setCategory}
          options={CATEGORIES.map((c) => ({ value: c, label: t(`files.category.${c}`) }))}
        />
        <Select
          className="w-40"
          value={status}
          onChange={setStatus}
          options={STATUSES.map((s) => ({ value: s, label: t(`files.status.${s}`) }))}
        />
      </div>

      {searching ? (
        <Entries
          key={`search:${q}:${rootId}:${category}:${status}`}
          queryKey={[...filesKeys.catalog(), 'search', q, rootId, category, status]}
          load={(skip) => filesService.search({ q: q || undefined, rootId, category, status }, skip)}
          roots={allRoots}
          showLocation
          onOpenFolder={openFolder}
        />
      ) : (
        <>
          <Breadcrumb
            items={[
              { title: <a onClick={() => setPath('/')}>{browsed.name}</a> },
              ...path
                .split('/')
                .filter(Boolean)
                .map((name, i, parts) => {
                  const to = '/' + parts.slice(0, i + 1).join('/')
                  return { title: <a onClick={() => setPath(to)}>{name}</a> }
                }),
            ]}
          />
          <Entries
            key={`browse:${browsed.id}:${path}`}
            queryKey={[...filesKeys.catalog(), 'browse', browsed.id, path]}
            load={(skip) => filesService.browse(browsed.id, path, skip)}
            roots={allRoots}
            onOpenFolder={openFolder}
          />
        </>
      )}
    </div>
  )
}

function Entries({
  queryKey,
  load,
  roots,
  showLocation = false,
  onOpenFolder,
}: {
  queryKey: readonly unknown[]
  load: (skip: number) => Promise<Page<Entry>>
  roots: Root[]
  showLocation?: boolean
  onOpenFolder: (entry: Entry) => void
}) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { agent, isHere } = useFilesAgent()
  const pages = useInfiniteQuery({
    queryKey,
    queryFn: ({ pageParam }) => load(pageParam),
    initialPageParam: 0,
    getNextPageParam: (last, all) => (last.hasMore ? all.reduce((n, p) => n + p.items.length, 0) : undefined),
  })
  const entries = pages.data?.pages.flatMap((p) => p.items) ?? []
  const rootOf = (id: string) => roots.find((r) => r.id === id)

  async function reveal(entry: Entry) {
    try {
      await agent!.invoke('files.reveal', { rootId: entry.rootId, path: entry.relativePath })
    } catch (e) {
      message.error(toErrorMessage(e, t('files.catalog.revealError')))
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <Table<Entry>
        rowKey="id"
        size="small"
        pagination={false}
        loading={pages.isLoading}
        scroll={{ x: true }}
        dataSource={entries}
        locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('files.catalog.empty')} /> }}
        columns={[
          {
            title: t('files.catalog.name'),
            render: (_, e) =>
              e.kind === 'directory' ? (
                <a onClick={() => onOpenFolder(e)}>
                  <FolderFilled className="mr-2 text-amber-500" />
                  {e.name}
                </a>
              ) : (
                <span>
                  <FileOutlined className="mr-2" />
                  {e.name}
                </span>
              ),
          },
          ...(showLocation
            ? [
                {
                  title: t('files.catalog.location'),
                  render: (_: unknown, e: Entry) => (
                    <Typography.Text type="secondary">
                      {rootOf(e.rootId)?.name ?? '?'} {e.parentPath}
                    </Typography.Text>
                  ),
                },
              ]
            : []),
          {
            title: t('files.catalog.size'),
            align: 'right' as const,
            render: (_, e) => (e.kind === 'file' ? formatBytes(e.sizeBytes) : ''),
          },
          {
            title: t('files.catalog.modified'),
            render: (_, e) => (e.modifiedAt ? new Date(e.modifiedAt).toLocaleString() : ''),
          },
          {
            title: '',
            render: (_, e) => {
              const root = rootOf(e.rootId)
              return (
                <span className="whitespace-nowrap">
                  {e.status !== 'present' && <Tag color={e.status === 'missing' ? 'orange' : 'default'}>{t(`files.status.${e.status}`)}</Tag>}
                  {root && isHere(root) && e.status === 'present' && (
                    <Tooltip title={t('files.catalog.reveal')}>
                      <Button type="text" size="small" aria-label={t('files.catalog.reveal')} icon={<FolderOpenOutlined />} onClick={() => void reveal(e)} />
                    </Tooltip>
                  )}
                  {root && (
                    <Typography.Text
                      copyable={{ text: localPathOf(root, e.relativePath), tooltips: [t('files.catalog.copyPath'), t('files.catalog.copied')] }}
                    />
                  )}
                </span>
              )
            },
          },
        ]}
      />
      {pages.hasNextPage && (
        <div className="text-center">
          <Button loading={pages.isFetchingNextPage} onClick={() => void pages.fetchNextPage()}>
            {t('files.catalog.more')}
          </Button>
        </div>
      )}
    </div>
  )
}
