import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Collapse, Drawer, Empty, Segmented, Select, Spin, theme } from 'antd'
import { diffLines } from 'diff'
import { toErrorMessage } from '@/lib/api/envelope'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { usePageVersion, usePageVersions, useRestorePageVersion } from '../hooks/usePageVersions'
import { usePageTree } from '../hooks/usePages'
import { useSpaces } from '../hooks/useSpaces'
import { groupVersions } from '../lib/pageHistory'
import { documentLines, type BlockDocument } from '../lib/blocks'
import { VisualEditor } from './visual/VisualEditor'
import type { PageDto, PageSnapshot } from '../models'

interface PageHistoryDrawerProps {
  pageId: string
  onClose: () => void
  onRestored: (page: PageDto) => void
}

const METADATA_FIELDS = {
  title: 'title',
  icon: 'icon',
  space: 'spaceId',
  parent: 'parentId',
  order: 'orderIndex',
  favorite: 'isFavorite',
  archived: 'archivedAt',
  deleted: 'deletedAt',
} as const

/** A version's blocks; the snapshot keeps the document as JSON text. */
function readBlocks(snapshot: PageSnapshot) {
  return (JSON.parse(snapshot.content || '{"blocks":[]}') as BlockDocument).blocks
}

export function PageHistoryDrawer({ pageId, onClose, onRestored }: PageHistoryDrawerProps) {
  const { t, i18n } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const { timeZone } = usePreferences()
  const history = usePageVersions(pageId)
  const versions = history.data?.pages.flat() ?? []
  const latest = versions[0]?.number
  const [selected, setSelected] = useState<number>()
  const [compared, setCompared] = useState<number>()
  const [mode, setMode] = useState<'diff' | 'preview'>('diff')
  const targetNumber = selected ?? latest
  const baseNumber = compared ?? versions[1]?.number ?? latest
  const target = usePageVersion(pageId, targetNumber)
  const base = usePageVersion(pageId, baseNumber)
  const restore = useRestorePageVersion(pageId)
  const { data: spaces } = useSpaces(true)
  const { data: pages } = usePageTree(true)
  const groups = groupVersions(versions)
  const parts = useMemo(() => {
    if (!target.data || !base.data) return []
    const titleOf = (id: string) => pages?.find((page) => page.id === id)?.title
    const text = (snapshot: PageSnapshot) => documentLines(readBlocks(snapshot), titleOf).join('\n')
    return diffLines(text(base.data.snapshot), text(target.data.snapshot), { timeout: 1000 })
  }, [target.data, base.data, pages])

  function date(value: string) {
    return new Intl.DateTimeFormat(i18n.language, {
      dateStyle: 'short', timeStyle: 'medium', timeZone,
    }).format(new Date(value))
  }

  function metadata(snapshot: PageSnapshot) {
    return {
      title: snapshot.title,
      icon: snapshot.icon ?? '—',
      space: spaces?.find((space) => space.id === snapshot.spaceId)?.name ?? snapshot.spaceId,
      parent: snapshot.parentId
        ? pages?.find((page) => page.id === snapshot.parentId)?.title ?? snapshot.parentId
        : t('notes.history.root'),
      order: String(snapshot.orderIndex),
      favorite: t(snapshot.isFavorite ? 'notes.history.yes' : 'notes.history.no'),
      archived: snapshot.archivedAt ? `${t('notes.history.yes')} (${date(snapshot.archivedAt)})` : t('notes.history.no'),
      deleted: snapshot.deletedAt ? `${t('notes.history.yes')} (${date(snapshot.deletedAt)})` : t('notes.history.no'),
    }
  }

  async function restoreSelected() {
    if (targetNumber === undefined || latest === undefined) return
    try {
      const page = await restore.mutateAsync({ number: targetNumber, expectedVersion: latest })
      onRestored(page)
      message.success(t('notes.history.restored'))
    } catch (error) {
      message.error(toErrorMessage(error, t('notes.history.restoreError')))
      void history.refetch()
    }
  }

  function confirmRestore() {
    modal.confirm({
      title: t('notes.history.restoreConfirm', { number: targetNumber }),
      content: t('notes.history.restoreDescription'),
      okText: t('notes.history.restore'),
      cancelText: t('common.cancel'),
      onOk: restoreSelected,
    })
  }

  function selectVersion(number: number) {
    setSelected(number)
    setCompared(latest)
  }

  function retryComparison() {
    void target.refetch()
    void base.refetch()
  }

  const options = versions.map((version) => ({ value: version.number, label: `v${version.number} · ${date(version.createdAt)}` }))
  const before = base.data && metadata(base.data.snapshot)
  const after = target.data && metadata(target.data.snapshot)

  function renderComparison() {
    if (target.isError || base.isError) {
      return <Button onClick={retryComparison}>{t('notes.history.retry')}</Button>
    }
    if (target.isPending || base.isPending) return <Spin />
    if (!target.data || !base.data || !before || !after) return null

    const from = base.data.snapshot
    const to = target.data.snapshot
    const changedFields = (Object.keys(METADATA_FIELDS) as (keyof typeof METADATA_FIELDS)[])
      .filter((key) => from[METADATA_FIELDS[key]] !== to[METADATA_FIELDS[key]])

    return (
      <>
        <table className="w-full text-left text-sm">
          <thead>
            <tr><th>{t('notes.history.field')}</th><th>v{baseNumber}</th><th>v{targetNumber}</th></tr>
          </thead>
          <tbody>
            {changedFields.map((key) => (
              <tr key={key}>
                <th scope="row">{t(`notes.history.fields.${key}`)}</th>
                <td className="break-words">{before[key]}</td>
                <td className="break-words">{after[key]}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {mode === 'preview' ? (
          <div className="notes-doc">
            <h2>{to.icon} {to.title}</h2>
            <VisualEditor
              key={targetNumber}
              docId={`${pageId}-v${targetNumber}`}
              initialContent={readBlocks(to)}
              pages={pages ?? []}
              editable={false}
              onOpenPage={() => undefined}
            />
          </div>
        ) : (
          <div>
            <p className="text-xs" style={{ color: token.colorTextSecondary }}>{t('notes.history.legend')}</p>
            {parts ? (
              <pre className="overflow-auto rounded p-3 text-xs" style={{ background: token.colorFillQuaternary }}>
                {parts.map((part, index) => (
                  <span
                    key={index}
                    className="block whitespace-pre-wrap break-words"
                    style={{
                      background: part.added ? token.colorSuccessBg : part.removed ? token.colorErrorBg : undefined,
                      color: part.added ? token.colorSuccessText : part.removed ? token.colorErrorText : undefined,
                    }}
                  >
                    {part.value.split('\n').map((line, lineIndex, lines) => {
                      if (lineIndex === lines.length - 1 && line === '') return null
                      return (
                        <span key={lineIndex} className="block">
                          {part.added ? '+ ' : part.removed ? '− ' : '  '}{line || ' '}
                        </span>
                      )
                    })}
                  </span>
                ))}
              </pre>
            ) : <p>{t('notes.history.diffTooLarge')}</p>}
          </div>
        )}
      </>
    )
  }

  function renderHistory() {
    if (history.isError) {
      return <Button onClick={() => void history.refetch()}>{t('notes.history.retry')}</Button>
    }
    if (history.isPending) return <Spin />
    if (!versions.length) return <Empty />

    return (
      <div className="flex flex-col gap-4">
        <Collapse
          className="max-h-56 overflow-auto"
          size="small"
          items={groups.map((group) => ({
            key: group[0].number,
            label: `${date(group[0].createdAt)} · ${t(`notes.history.source.${group[0].source}`)} · ${t('notes.history.versionCount', { count: group.length })}`,
            children: (
              <div className="flex flex-col items-start gap-1">
                {group.map((version) => (
                  <Button
                    key={version.number}
                    type={targetNumber === version.number ? 'primary' : 'text'}
                    size="small"
                    onClick={() => selectVersion(version.number)}
                  >
                    v{version.number} · {date(version.createdAt)}{version.restoredFrom != null && ` ← v${version.restoredFrom}`}
                  </Button>
                ))}
              </div>
            ),
          }))}
        />
        {history.hasNextPage && (
          <Button loading={history.isFetchingNextPage} onClick={() => void history.fetchNextPage()}>
            {t('notes.history.loadMore')}
          </Button>
        )}
        <div className="flex flex-wrap gap-3">
          <label className="min-w-0 flex-1">
            {t('notes.history.from')}
            <Select className="w-full" aria-label={t('notes.history.from')} value={baseNumber} options={options} onChange={setCompared} />
          </label>
          <label className="min-w-0 flex-1">
            {t('notes.history.to')}
            <Select className="w-full" aria-label={t('notes.history.to')} value={targetNumber} options={options} onChange={setSelected} />
          </label>
        </div>
        <Segmented
          value={mode}
          onChange={setMode}
          options={[
            { value: 'diff', label: t('notes.history.diff') },
            { value: 'preview', label: t('notes.history.preview') },
          ]}
        />
        {renderComparison()}
      </div>
    )
  }

  return (
    <Drawer
      title={t('notes.history.title')}
      open
      onClose={onClose}
      size="large"
      styles={{ wrapper: { maxWidth: '100vw' } }}
      extra={(
        <Button
          type="primary"
          loading={restore.isPending}
          disabled={!target.data || targetNumber === latest || !!target.data.snapshot.deletedAt}
          onClick={confirmRestore}
        >
          {t('notes.history.restore')}
        </Button>
      )}
    >
      <p style={{ color: token.colorTextSecondary }}>{t('notes.history.description')}</p>
      {renderHistory()}
    </Drawer>
  )
}
