import { useState } from 'react'
import { App, Breadcrumb, Button, DatePicker, Empty, Input, InputNumber, Select, Space, Spin, Table, Tag, Tooltip, Typography } from 'antd'
import { FileOutlined, FolderFilled, FolderOpenOutlined } from '@ant-design/icons'
import { Link } from 'react-router-dom'
import { useInfiniteQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import { FilesGate } from '../components/FilesGate'
import { filesKeys, useFilesAgent, useRoots } from '../hooks/useFiles'
import { formatBytes, formatDuration, localPathOf } from '../lib/format'
import * as filesService from '../services/files.service'
import type { Entry, EntryStatus, FileCategory, FileMetadata, Page, Resolution, Root, SearchCriteria } from '../models'

const CATEGORIES: FileCategory[] = ['video', 'audio', 'image', 'document', 'ebook', 'archive', 'code', 'other']
const STATUSES: (EntryStatus | 'all')[] = ['present', 'missing', 'excluded', 'all']
const RESOLUTIONS: Resolution[] = ['sd', 'hd', 'full-hd', '4k']

/** The criteria a file's metadata answers; offered by category, and cleared when it changes. */
type MetadataCriteria = Pick<SearchCriteria, 'takenFrom' | 'takenTo' | 'resolution' | 'minDuration' | 'maxDuration'>

/**
 * The catalog from any device: browse a root folder by folder, or search every root by fragments of a
 * name, title or artist, narrowed by what the files' metadata says.
 */
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
  const [meta, setMeta] = useState<MetadataCriteria>({})
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

  const searching =
    q.trim() !== '' || category !== undefined || status !== 'present' || Object.values(meta).some((v) => v !== undefined)
  const browsed = allRoots.find((r) => r.id === rootId) ?? allRoots[0]

  function openFolder(entry: Entry) {
    setText('')
    setQ('')
    setCategory(undefined)
    setMeta({})
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
          onChange={(c) => {
            setCategory(c)
            setMeta({})
          }}
          options={CATEGORIES.map((c) => ({ value: c, label: t(`files.category.${c}`) }))}
        />
        {category === 'image' && (
          <DatePicker.RangePicker
            allowEmpty={[true, true]}
            placeholder={[t('files.catalog.takenFrom'), t('files.catalog.takenTo')]}
            onChange={(range) =>
              setMeta({ takenFrom: range?.[0]?.format('YYYY-MM-DD'), takenTo: range?.[1]?.format('YYYY-MM-DD') })
            }
          />
        )}
        {category === 'video' && (
          <Select
            className="w-44"
            value={meta.resolution}
            placeholder={t('files.catalog.anyResolution')}
            allowClear
            onChange={(resolution) => setMeta((m) => ({ ...m, resolution }))}
            options={RESOLUTIONS.map((r) => ({ value: r, label: t(`files.catalog.resolution.${r}`) }))}
          />
        )}
        {(category === 'video' || category === 'audio') && (
          <Space.Compact>
            <InputNumber
              className="w-32"
              min={0}
              placeholder={t('files.catalog.minMinutes')}
              aria-label={t('files.catalog.minMinutes')}
              onChange={(v) => setMeta((m) => ({ ...m, minDuration: v == null ? undefined : v * 60 }))}
            />
            <InputNumber
              className="w-32"
              min={0}
              placeholder={t('files.catalog.maxMinutes')}
              aria-label={t('files.catalog.maxMinutes')}
              onChange={(v) => setMeta((m) => ({ ...m, maxDuration: v == null ? undefined : v * 60 }))}
            />
          </Space.Compact>
        )}
        <Select
          className="w-40"
          value={status}
          onChange={setStatus}
          options={STATUSES.map((s) => ({ value: s, label: t(`files.status.${s}`) }))}
        />
      </div>

      {searching ? (
        <Entries
          key={`search:${q}:${rootId}:${category}:${status}:${JSON.stringify(meta)}`}
          queryKey={[...filesKeys.catalog(), 'search', q, rootId, category, status, meta]}
          load={(skip) => filesService.search({ q: q || undefined, rootId, category, status, ...meta }, skip)}
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
                <div>
                  <FileOutlined className="mr-2" />
                  {e.name}
                  {e.metadata && <MetadataLine metadata={e.metadata} />}
                </div>
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

/** What the bytes say about a file, in one line under its name, with a map link when it has a place. */
function MetadataLine({ metadata: m }: { metadata: FileMetadata }) {
  const { t } = useTranslation()
  const facts = [
    m.takenAt && new Date(m.takenAt).toLocaleString(),
    m.camera,
    m.width && m.height && `${m.width}×${m.height}`,
    m.durationSeconds && formatDuration(m.durationSeconds),
    [m.artist, m.title].filter(Boolean).join(' — '),
    m.album,
    m.pages && t('files.catalog.pages', { count: m.pages }),
  ].filter(Boolean)
  const located = m.latitude != null && m.longitude != null
  if (facts.length === 0 && !located) return null

  return (
    <Typography.Text type="secondary" className="block text-xs">
      {facts.join(' · ')}
      {located && (
        <>
          {facts.length > 0 && ' · '}
          <a
            href={`https://www.openstreetmap.org/?mlat=${m.latitude}&mlon=${m.longitude}#map=16/${m.latitude}/${m.longitude}`}
            target="_blank"
            rel="noreferrer"
          >
            {t('files.catalog.onMap')}
          </a>
        </>
      )}
    </Typography.Text>
  )
}
