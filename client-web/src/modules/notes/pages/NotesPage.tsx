import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import { Alert, App, Button, Drawer, Empty, Grid, Segmented, Spin, Tooltip, theme } from 'antd'
import {
  CheckOutlined,
  CodeOutlined,
  EditOutlined,
  EyeOutlined,
  InfoCircleOutlined,
  HistoryOutlined,
  MenuFoldOutlined,
  MenuOutlined,
  MenuUnfoldOutlined,
  PictureOutlined,
  SplitCellsOutlined,
} from '@ant-design/icons'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { toErrorMessage } from '@/lib/api/envelope'
import { BacklinksPanel } from '../components/BacklinksPanel'
import { LocalGraphPanel } from '../components/LocalGraphPanel'
import { MarkdownEditor, type MarkdownEditorHandle } from '../components/MarkdownEditor'
import { MarkdownPreview } from '../components/MarkdownPreview'
import { NotesSidebar } from '../components/NotesSidebar'
import { PageBreadcrumb } from '../components/PageBreadcrumb'
import { PageHeader } from '../components/PageHeader'
import { PageHistoryDrawer } from '../components/PageHistoryDrawer'
import { SearchPalette } from '../components/SearchPalette'
import { SpaceOverview } from '../components/SpaceOverview'
import { VisualEditor } from '../components/visual/VisualEditor'
import { useAutosave } from '../hooks/useAutosave'
import { useCreatePage, usePage, usePageTree, useUpdatePage } from '../hooks/usePages'
import { useSpaces } from '../hooks/useSpaces'
import { useUploadMedia } from '../hooks/useMedia'
import { useTags } from '../hooks/useTags'
import type { PageDto } from '../models'
import { buildPageIndex } from '../lib/wikilinks'
import '../components/notes.css'

/** `visual` writes on the page itself; `edit` and `split` write the markdown, the latter beside the reading. */
type ViewMode = 'visual' | 'edit' | 'split' | 'preview'

const SIDEBAR_WIDTH = 272
const VIEW_MODES: ViewMode[] = ['visual', 'edit', 'split', 'preview']
const VIEW_MODE_KEY = 'pandora.notes_view_mode'
const SIDEBAR_KEY = 'pandora.notes_sidebar_closed'

// Remembered per device: how one likes to write and whether the tree is in the way are habits.
function readStored<T extends string>(key: string, allowed: readonly T[], fallback: T): T {
  try {
    const value = localStorage.getItem(key)
    return value && (allowed as readonly string[]).includes(value) ? (value as T) : fallback
  } catch {
    return fallback
  }
}

function store(key: string, value: string) {
  try {
    localStorage.setItem(key, value)
  } catch {
    // Private mode or blocked storage: the choice just lasts until the reload.
  }
}

/** What autosave writes: the whole editable surface of a page. */
interface PageDraft {
  title: string
  icon: string | null
  contentMarkdown: string
}

export function NotesPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const { isDark } = usePreferences()
  const navigate = useNavigate()
  const { id: routeId, spaceId: routeSpaceId } = useParams<{ id: string; spaceId: string }>()
  const screens = Grid.useBreakpoint()
  // Until the first measure lands, assume the wide layout: a phone settles a frame later.
  const isMobile = screens.md === false

  const selectedId = routeId ?? null
  const [includeArchived, setIncludeArchived] = useState(false)
  const [tagIds, setTagIds] = useState<string[]>([])
  const [searchOpen, setSearchOpen] = useState(false)
  const [viewMode, setViewMode] = useState<ViewMode>(() => readStored(VIEW_MODE_KEY, VIEW_MODES, 'visual'))
  const [sidebarClosed, setSidebarClosed] = useState(() => readStored(SIDEBAR_KEY, ['1', '0'], '0') === '1')
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [infoOpen, setInfoOpen] = useState(false)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [editorGeneration, setEditorGeneration] = useState(0)
  // A phone opens a page to read it; editing is a step it takes on purpose.
  const [mobileEditing, setMobileEditing] = useState(false)
  const editorRef = useRef<MarkdownEditorHandle>(null)

  // Local draft of the open page; the source of truth while typing.
  const [draft, setDraft] = useState<PageDraft>({ title: '', icon: null, contentMarkdown: '' })

  const { data: page, isLoading, error: pageError } = usePage(selectedId)

  // The space comes from the URL on its own page, and from the open page otherwise.
  const spaceId = routeSpaceId ?? page?.spaceId ?? null
  const { data: spaces, error: spacesError } = useSpaces(true)
  const space = spaces?.find((candidate) => candidate.id === spaceId)
  const updateMutation = useUpdatePage()
  const createMutation = useCreatePage()
  const uploadMedia = useUploadMedia()

  // Wikilinks resolve against every page, archived included: archiving hides a page from the
  // sidebar but does not break links to it (and the backend resolves it the same way).
  const { data: allPages, error: treeError } = usePageTree(true)
  const pageIndex = useMemo(() => buildPageIndex(allPages ?? [], spaceId), [allPages, spaceId])

  // The tags exist module-wide: the preview paints the `#tag` chips with them, and clicking one
  // (in the body or in the header) is what fills the sidebar filter.
  const { tags, index: tagIndex, error: tagsError } = useTags()

  function filterByTag(tagId: string) {
    setTagIds([tagId])
    if (isMobile) setDrawerOpen(true)
    else if (sidebarClosed) toggleSidebar()
  }

  function filterByTagSlug(slug: string) {
    const tag = tags.find((candidate) => candidate.slug === slug)
    if (tag) filterByTag(tag.id)
  }

  function changeViewMode(mode: ViewMode) {
    setViewMode(mode)
    store(VIEW_MODE_KEY, mode)
  }

  const toggleSidebar = useCallback(() => {
    setSidebarClosed((closed) => {
      store(SIDEBAR_KEY, closed ? '0' : '1')
      return !closed
    })
  }, [])

  // Ctrl+\ (Cmd+\ on a Mac) folds the tree away to write, as in Notion.
  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if ((event.ctrlKey || event.metaKey) && event.key === '\\') {
        event.preventDefault()
        toggleSidebar()
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [toggleSidebar])

  // The page the draft belongs to — also the id autosave writes to. It trails `selectedId`
  // while the next page loads, so a flush during a switch still targets the page being left.
  const [draftPageId, setDraftPageId] = useState<string | null>(null)

  // Seed the draft when a different page arrives (render-phase adjustment, not an effect).
  if (page && page.id !== draftPageId) {
    setDraftPageId(page.id)
    setDraft({ title: page.title, icon: page.icon, contentMarkdown: page.contentMarkdown })
    setMobileEditing(false)
  }

  const handleSave = useCallback(
    async (value: PageDraft) => {
      if (!draftPageId) return
      await updateMutation.mutateAsync({
        id: draftPageId,
        body: { title: value.title, icon: value.icon, contentMarkdown: value.contentMarkdown },
      })
    },
    [updateMutation, draftPageId],
  )

  const { status, schedule, flush, reset } = useAutosave<PageDraft>({ onSave: handleSave })

  async function openHistory() {
    try {
      await flush()
      setHistoryOpen(true)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
    }
  }

  function handleRestored(restored: PageDto) {
    reset()
    setDraft({ title: restored.title, icon: restored.icon, contentMarkdown: restored.contentMarkdown })
    setEditorGeneration((generation) => generation + 1)
    setHistoryOpen(false)
  }

  // Selecting another page: write pending edits to the page being left, then navigate.
  async function handleSelect(id: string | null) {
    setDrawerOpen(false)
    setInfoOpen(false)
    if (id === selectedId) return
    try {
      await flush()
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
      return
    }
    if (id) navigate(`/notes/${id}`)
    else {
      reset()
      setDraftPageId(null)
      navigate(spaceId ? `/notes/s/${spaceId}` : '/notes')
    }
  }

  async function handleSpaceChange(id: string) {
    setDrawerOpen(false)
    try {
      await flush()
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
      return
    }
    reset()
    setDraftPageId(null)
    navigate(`/notes/s/${id}`)
  }

  async function handleCreateInSpace() {
    try {
      const created = await createMutation.mutateAsync({ title: t('notes.untitled'), spaceId })
      await handleSelect(created.id)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
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
      const created = await createMutation.mutateAsync({ title, spaceId })
      await handleSelect(created.id)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
    }
  }

  async function handleUpload(file: File) {
    try {
      // Pasted or dropped into a note, a file lands in the library unsorted.
      return await uploadMedia.mutateAsync({ file })
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

  // A phone has no room for two panes: it reads, or it edits.
  const mode: ViewMode = isMobile ? (mobileEditing ? 'visual' : 'preview') : viewMode
  const border = `1px solid ${token.colorBorderSecondary}`

  const sidebar = (
    <NotesSidebar
      spaceId={spaceId}
      onSpaceChange={handleSpaceChange}
      selectedId={selectedId}
      onSelect={handleSelect}
      includeArchived={includeArchived}
      onToggleArchived={setIncludeArchived}
      tagIds={tagIds}
      onTagIdsChange={setTagIds}
      onSearch={() => {
        setDrawerOpen(false)
        setSearchOpen(true)
      }}
    />
  )

  // Both editors start from the draft, not the saved page: switching modes carries what is not saved yet.
  const editor = (autoHeight: boolean) =>
    selectedId && (
      <MarkdownEditor
        key={`${selectedId}-${editorGeneration}`}
        docId={selectedId}
        initialValue={draft.contentMarkdown}
        placeholder={t('notes.contentPlaceholder')}
        isDark={isDark}
        pages={allPages ?? []}
        tags={tags}
        onChange={(value) => updateDraft({ contentMarkdown: value })}
        onUpload={handleUpload}
        autoHeight={autoHeight}
        ref={editorRef}
      />
    )

  const visualEditor = selectedId && (
    <VisualEditor
      key={`${selectedId}-${editorGeneration}`}
      docId={selectedId}
      initialValue={draft.contentMarkdown}
      pages={allPages ?? []}
      tags={tags}
      pageIndex={pageIndex}
      tagIndex={tagIndex}
      onChange={(value) => updateDraft({ contentMarkdown: value })}
      onUpload={handleUpload}
      onOpenPage={handleSelect}
      onCreatePage={handleCreateFromLink}
      onSelectTag={filterByTagSlug}
      ref={editorRef}
    />
  )

  const preview = (
    <MarkdownPreview
      markdown={draft.contentMarkdown}
      pageIndex={pageIndex}
      tagIndex={tagIndex}
      onOpenPage={handleSelect}
      onCreatePage={handleCreateFromLink}
      onSelectTag={filterByTagSlug}
    />
  )

  const header = page && (
    <PageHeader
      icon={draft.icon}
      title={draft.title}
      editedAt={page.updatedAt ?? page.createdAt}
      status={statusLabel[status]}
      statusIsError={status === 'error'}
      tags={page.tags}
      editable={!isMobile || mobileEditing}
      onIconChange={(icon) => updateDraft({ icon })}
      onTitleChange={(title) => updateDraft({ title })}
      onSelectTag={filterByTag}
    />
  )

  const toolbar = (
    <div className="flex h-12 shrink-0 items-center gap-2 px-3" style={{ borderBottom: border }}>
      {isMobile ? (
        <Button type="text" icon={<MenuOutlined />} aria-label={t('notes.openSidebar')} onClick={() => setDrawerOpen(true)} />
      ) : (
        <Tooltip title={t(sidebarClosed ? 'notes.showSidebar' : 'notes.hideSidebar')}>
          <Button
            type="text"
            icon={sidebarClosed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
            aria-label={t(sidebarClosed ? 'notes.showSidebar' : 'notes.hideSidebar')}
            onClick={toggleSidebar}
          />
        </Tooltip>
      )}

      <div className="min-w-0 flex-1">
        <PageBreadcrumb
          space={space}
          page={page && selectedId ? { ...page, title: draft.title, icon: draft.icon } : null}
          pages={allPages ?? []}
          compact={isMobile}
          onOpenSpace={() => handleSelect(null)}
          onOpenPage={handleSelect}
        />
      </div>

      {selectedId && page && (
        <>
          {isMobile ? (
            <Button
              type={mobileEditing ? 'primary' : 'default'}
              size="small"
              icon={mobileEditing ? <CheckOutlined /> : <EditOutlined />}
              onClick={() => {
                if (mobileEditing) void flush().catch((err) => message.error(toErrorMessage(err, t('notes.saveError'))))
                setMobileEditing(!mobileEditing)
              }}
            >
              {t(mobileEditing ? 'notes.doneEditing' : 'notes.edit')}
            </Button>
          ) : (
            <Segmented
              size="small"
              value={viewMode}
              onChange={(value) => changeViewMode(value as ViewMode)}
              aria-label={t('notes.viewMode')}
              options={[
                { value: 'visual', icon: <EditOutlined />, title: t('notes.viewVisual') },
                { value: 'edit', icon: <CodeOutlined />, title: t('notes.viewEdit') },
                { value: 'split', icon: <SplitCellsOutlined />, title: t('notes.viewSplit') },
                { value: 'preview', icon: <EyeOutlined />, title: t('notes.viewPreview') },
              ]}
            />
          )}
          {mode !== 'preview' && (
            <Tooltip title={t('notes.media.insert')}>
              <Button
                type="text"
                icon={<PictureOutlined />}
                aria-label={t('notes.media.insert')}
                onClick={() => editorRef.current?.insertMedia()}
              />
            </Tooltip>
          )}
          <Tooltip title={t('notes.info')}>
            <Button
              type={infoOpen ? 'default' : 'text'}
              icon={<InfoCircleOutlined />}
              aria-label={t('notes.info')}
              onClick={() => setInfoOpen(!infoOpen)}
            />
          </Tooltip>
          <Tooltip title={t('notes.history.title')}>
            <Button
              type="text"
              icon={<HistoryOutlined />}
              aria-label={t('notes.history.title')}
              onClick={() => void openHistory()}
            />
          </Tooltip>
        </>
      )}
    </div>
  )

  let body: ReactNode
  const loadError = pageError ?? spacesError ?? treeError ?? tagsError
  if (loadError) {
    body = <Alert type="error" showIcon title={loadError.message} />
  } else if (!selectedId) {
    body = space ? (
      <SpaceOverview space={space} onOpenPage={handleSelect} onCreatePage={handleCreateInSpace} />
    ) : (
      <div className="flex h-full items-center justify-center">
        <Empty description={t('notes.noSelection')} />
      </div>
    )
  } else if (isLoading) {
    body = (
      <div className="flex h-full items-center justify-center">
        <Spin />
      </div>
    )
  } else if (!page) {
    body = <Empty description={t('notes.pageUnavailable')} />
  } else if (mode === 'split') {
    // Side by side, each pane scrolls on its own; the title sits above both.
    body = (
      <div className="flex h-full flex-col">
        <div className="notes-doc shrink-0 px-8 pt-6">{header}</div>
        <div className="flex min-h-0 flex-1" style={{ borderTop: border }}>
          <div className="min-w-0 flex-1" style={{ borderRight: border }}>
            {editor(false)}
          </div>
          <div className="notes-doc min-w-0 flex-1 overflow-auto px-6 py-4">{preview}</div>
        </div>
      </div>
    )
  } else {
    // One column at a reading width, scrolling as a whole document.
    body = (
      <div className="h-full overflow-auto">
        <div className="notes-doc mx-auto w-full max-w-[760px] px-5 py-8 md:px-10 md:py-12">
          {header}
          {mode === 'edit' ? editor(true) : mode === 'visual' ? visualEditor : preview}
        </div>
      </div>
    )
  }

  return (
    <div className="flex gap-4" style={{ height: 'calc(100dvh - 112px)' }}>
      <SearchPalette open={searchOpen} onOpenChange={setSearchOpen} onSelect={handleSelect} spaceId={spaceId} />

      {isMobile ? (
        <Drawer
          placement="left"
          open={drawerOpen}
          onClose={() => setDrawerOpen(false)}
          size={300}
          closable={false}
          styles={{ body: { padding: 12 } }}
        >
          {sidebar}
        </Drawer>
      ) : (
        !sidebarClosed && (
          <aside
            className="h-full shrink-0 overflow-hidden rounded-xl p-3"
            style={{ width: SIDEBAR_WIDTH, border, background: token.colorBgContainer }}
          >
            {sidebar}
          </aside>
        )
      )}

      <main
        className="flex h-full min-w-0 flex-1 flex-col overflow-hidden rounded-xl"
        style={{ border, background: token.colorBgContainer }}
      >
        {toolbar}
        <div className="min-h-0 flex-1">{body}</div>
      </main>

      {selectedId && (
        <Drawer
          title={t('notes.info')}
          placement={isMobile ? 'bottom' : 'right'}
          open={infoOpen}
          onClose={() => setInfoOpen(false)}
          size={isMobile ? '75%' : 380}
          styles={{ body: { padding: 0 } }}
        >
          <BacklinksPanel pageId={selectedId} onSelect={handleSelect} />
          <LocalGraphPanel pageId={selectedId} onSelect={handleSelect} />
        </Drawer>
      )}
      {selectedId && historyOpen && (
        <PageHistoryDrawer
          key={selectedId}
          pageId={selectedId}
          onClose={() => setHistoryOpen(false)}
          onRestored={handleRestored}
        />
      )}
    </div>
  )
}
