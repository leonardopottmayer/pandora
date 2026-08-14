import { useCallback, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import { App, Card, Empty, Flex, Input, Segmented, Spin, Tooltip, Typography, theme } from 'antd'
import { EditOutlined, EyeOutlined, SplitCellsOutlined } from '@ant-design/icons'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { toErrorMessage } from '@/lib/api/envelope'
import { BacklinksPanel } from '../components/BacklinksPanel'
import { LocalGraphPanel } from '../components/LocalGraphPanel'
import { MarkdownEditor } from '../components/MarkdownEditor'
import { MarkdownPreview } from '../components/MarkdownPreview'
import { NotesSidebar } from '../components/NotesSidebar'
import { SearchPalette } from '../components/SearchPalette'
import { useAutosave } from '../hooks/useAutosave'
import { useCreatePage, usePage, usePageTree, useUpdatePage } from '../hooks/usePages'
import { buildPageIndex } from '../lib/wikilinks'
import { uploadAttachment } from '../services/attachments.service'
import '../components/notes.css'

type ViewMode = 'edit' | 'split' | 'preview'

const SIDEBAR_WIDTH = 280

/** What autosave writes: the whole editable surface of a page. */
interface PageDraft {
  title: string
  contentMarkdown: string
}

export function NotesPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const { isDark } = usePreferences()
  const navigate = useNavigate()
  const { id: routeId } = useParams<{ id: string }>()

  const selectedId = routeId ?? null
  const [includeArchived, setIncludeArchived] = useState(false)
  const [searchOpen, setSearchOpen] = useState(false)
  const [viewMode, setViewMode] = useState<ViewMode>('split')

  // Local draft of the open page; the source of truth while typing.
  const [draft, setDraft] = useState<PageDraft>({ title: '', contentMarkdown: '' })

  const { data: page, isLoading } = usePage(selectedId)
  const updateMutation = useUpdatePage()
  const createMutation = useCreatePage()

  // Wikilinks resolve against every page, archived included: archiving hides a page from the
  // sidebar but does not break links to it (and the backend resolves it the same way).
  const { data: allPages } = usePageTree(true)
  const pageIndex = useMemo(() => buildPageIndex(allPages ?? []), [allPages])

  // The page the draft belongs to — also the id autosave writes to. It trails `selectedId`
  // while the next page loads, so a flush during a switch still targets the page being left.
  const [draftPageId, setDraftPageId] = useState<string | null>(null)

  // Seed the draft when a different page arrives (render-phase adjustment, not an effect).
  if (page && page.id !== draftPageId) {
    setDraftPageId(page.id)
    setDraft({ title: page.title, contentMarkdown: page.contentMarkdown })
  }

  const handleSave = useCallback(
    async (value: PageDraft) => {
      if (!draftPageId) return
      await updateMutation.mutateAsync({
        id: draftPageId,
        body: { title: value.title, icon: page?.icon ?? null, contentMarkdown: value.contentMarkdown },
      })
    },
    [updateMutation, draftPageId, page?.icon],
  )

  const { status, schedule, flush, reset } = useAutosave<PageDraft>({ onSave: handleSave })

  // Selecting another page: write pending edits to the page being left, then navigate.
  async function handleSelect(id: string | null) {
    if (id === selectedId) return
    await flush()
    if (id) navigate(`/notes/${id}`)
    else {
      reset()
      setDraftPageId(null)
      navigate('/notes')
    }
  }

  function updateDraft(patch: Partial<PageDraft>) {
    setDraft((current) => {
      const next = { ...current, ...patch }
      schedule(next)
      return next
    })
  }

  // Clicking a `[[link]]` that resolves to nothing: create that page, then open it.
  async function handleCreateFromLink(title: string) {
    try {
      const created = await createMutation.mutateAsync({ title })
      await handleSelect(created.id)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
    }
  }

  async function handleUpload(file: File) {
    try {
      return await uploadAttachment(file, selectedId ?? undefined)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.uploadError')))
      throw err
    }
  }

  const statusLabel: Record<typeof status, string> = {
    idle: '',
    pending: t('notes.unsaved'),
    saving: t('notes.saving'),
    saved: t('notes.saved'),
    error: t('notes.saveError'),
  }

  const editorPane = selectedId && (
    <MarkdownEditor
      docId={selectedId}
      initialValue={page?.contentMarkdown ?? ''}
      placeholder={t('notes.contentPlaceholder')}
      isDark={isDark}
      pages={allPages ?? []}
      onChange={(value) => updateDraft({ contentMarkdown: value })}
      onUpload={handleUpload}
    />
  )

  return (
    <Flex gap={16} align="stretch" style={{ height: 'calc(100vh - 112px)' }}>
      <SearchPalette open={searchOpen} onOpenChange={setSearchOpen} onSelect={handleSelect} />

      <Card
        styles={{ body: { padding: 12, height: '100%', overflow: 'hidden' } }}
        style={{ width: SIDEBAR_WIDTH, flexShrink: 0, height: '100%' }}
      >
        <NotesSidebar
          selectedId={selectedId}
          onSelect={handleSelect}
          includeArchived={includeArchived}
          onToggleArchived={setIncludeArchived}
          onSearch={() => setSearchOpen(true)}
        />
      </Card>

      <Card
        styles={{ body: { padding: 0, height: '100%', display: 'flex', flexDirection: 'column' } }}
        style={{ flex: 1, height: '100%', overflow: 'hidden' }}
      >
        {!selectedId ? (
          <Flex align="center" justify="center" style={{ height: '100%' }}>
            <Empty description={t('notes.noSelection')} />
          </Flex>
        ) : isLoading ? (
          <Flex align="center" justify="center" style={{ height: '100%' }}>
            <Spin />
          </Flex>
        ) : (
          <>
            <Flex
              align="center"
              gap={12}
              style={{ padding: '12px 16px', borderBottom: `1px solid ${token.colorBorderSecondary}` }}
            >
              <Input
                variant="borderless"
                value={draft.title}
                placeholder={t('notes.titlePlaceholder')}
                onChange={(e) => updateDraft({ title: e.target.value })}
                style={{ flex: 1, fontSize: 20, fontWeight: 600, padding: 0 }}
              />
              <Typography.Text type={status === 'error' ? 'danger' : 'secondary'} style={{ fontSize: 12 }}>
                {statusLabel[status]}
              </Typography.Text>
              <Tooltip title={t('notes.viewMode')}>
                <Segmented
                  size="small"
                  value={viewMode}
                  onChange={(value) => setViewMode(value as ViewMode)}
                  options={[
                    { value: 'edit', icon: <EditOutlined /> },
                    { value: 'split', icon: <SplitCellsOutlined /> },
                    { value: 'preview', icon: <EyeOutlined /> },
                  ]}
                />
              </Tooltip>
            </Flex>

            <Flex style={{ flex: 1, minHeight: 0 }}>
              {viewMode !== 'preview' && (
                <div
                  style={{
                    flex: 1,
                    minWidth: 0,
                    height: '100%',
                    borderRight:
                      viewMode === 'split' ? `1px solid ${token.colorBorderSecondary}` : undefined,
                  }}
                >
                  {editorPane}
                </div>
              )}
              {viewMode !== 'edit' && (
                <div style={{ flex: 1, minWidth: 0, height: '100%', overflow: 'auto' }}>
                  <MarkdownPreview
                    markdown={draft.contentMarkdown}
                    pageIndex={pageIndex}
                    onOpenPage={handleSelect}
                    onCreatePage={handleCreateFromLink}
                  />
                </div>
              )}
            </Flex>

            <BacklinksPanel pageId={selectedId} onSelect={handleSelect} />
            <LocalGraphPanel pageId={selectedId} onSelect={handleSelect} />
          </>
        )}
      </Card>
    </Flex>
  )
}
