import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { Alert, App, Badge, Button, Drawer, Empty, Grid, Spin, Tag, Tooltip, theme } from 'antd'
import {
  CheckOutlined,
  CommentOutlined,
  EditOutlined,
  LockOutlined,
  SafetyCertificateOutlined,
  InfoCircleOutlined,
  HistoryOutlined,
  MenuFoldOutlined,
  MenuOutlined,
  MenuUnfoldOutlined,
  PictureOutlined,
} from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import { BacklinksPanel } from '../components/BacklinksPanel'
import { PageCover } from '../components/PageCover'
import { PageMenu, type ExportKind } from '../components/PageMenu'
import { ShareModal } from '../components/ShareModal'
import { CommentComposer, CommentsPanel } from '../components/CommentsPanel'
import { useBlockStamps, useCommentChange, usePageComments } from '../hooks/useComments'
import * as commentsService from '../services/comments.service'
import { noteKeys } from '../hooks/queryKeys'
import { blocksToHtml, blocksToMarkdown } from '../lib/markdown'
import { downloadText, fileNameOf, mediaDataUrl } from '../lib/pageExport'
import { timeAgo } from '../lib/timeAgo'
import * as pagesService from '../services/pages.service'
import type { PageAppearance } from '../models'
import { LocalGraphPanel } from '../components/LocalGraphPanel'
import { NotesSidebar } from '../components/NotesSidebar'
import { FolderView } from '../components/FolderView'
import { PageBreadcrumb } from '../components/PageBreadcrumb'
import { PageFooter } from '../components/PageFooter'
import { PageToc } from '../components/PageToc'
import { PageHeader } from '../components/PageHeader'
import { PageHistoryDrawer } from '../components/PageHistoryDrawer'
import { SearchPalette } from '../components/SearchPalette'
import { SpaceOverview } from '../components/SpaceOverview'
import { VisualEditor, type VisualEditorHandle } from '../components/visual/VisualEditor'
import { useAutosave } from '../hooks/useAutosave'
import { useCreateFolder, useFolders } from '../hooks/useFolders'
import { useCreatePage, usePage, usePageSetting, usePageTree, useUpdatePage } from '../hooks/usePages'
import { useSpaces } from '../hooks/useSpaces'
import { useUploadMedia } from '../hooks/useMedia'
import { useTags } from '../hooks/useTags'
import type { PageDto } from '../models'
import { headingsOf, newBlockId, tagsOf, withTags, type Block } from '../lib/blocks'
import { getPage } from '../services/pages.service'
import { buildSpaceTree, findNode, pathTo, type SpaceNode } from '../lib/spaceTree'
import '../components/notes.css'

const SIDEBAR_WIDTH = 272
const SIDEBAR_KEY = 'pandora.notes_sidebar_closed'

// Remembered per device: whether the tree is in the way is a habit.
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
  /** Every block of the page, its tags block included. */
  blocks: Block[]
  /** Identifies an accepted AI change, so later manual saves return to the edit origin. */
  assistantRevision?: string
}

export function NotesPage() {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const navigate = useNavigate()
  const { id: routeId, spaceId: routeSpaceId, folderId: routeFolderId } = useParams<{ id: string; spaceId: string; folderId: string }>()
  const screens = Grid.useBreakpoint()
  // Until the first measure lands, assume the wide layout: a phone settles a frame later.
  const isMobile = screens.md === false

  const selectedId = routeId ?? null
  const selectedFolderId = routeFolderId ?? null
  const [includeArchived, setIncludeArchived] = useState(false)
  const [tagIds, setTagIds] = useState<string[]>([])
  const [searchOpen, setSearchOpen] = useState(false)
  const [sidebarClosed, setSidebarClosed] = useState(() => readStored(SIDEBAR_KEY, ['1', '0'], '0') === '1')
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [infoOpen, setInfoOpen] = useState(false)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [editorGeneration, setEditorGeneration] = useState(0)
  // A phone opens a page to read it; editing is a step it takes on purpose.
  const [mobileEditing, setMobileEditing] = useState(false)
  const editorRef = useRef<VisualEditorHandle>(null)
  // The element the page scrolls in: the table of contents follows the reading through it.
  const [scroller, setScroller] = useState<HTMLDivElement | null>(null)

  // Local draft of the open page; the source of truth while typing.
  const [draft, setDraft] = useState<PageDraft>({ title: '', icon: null, blocks: [] })
  const savedAssistantRevisions = useRef(new Map<string, string>())

  const { data: page, isLoading, error: pageError } = usePage(selectedId)

  // Every folder, so an opened folder (whose URL names no space) is known before its space is.
  const { data: allFolders = [], error: foldersError } = useFolders(null)
  const folder = selectedFolderId ? allFolders.find((candidate) => candidate.id === selectedFolderId) : undefined

  // The space comes from the URL on its own page, and from the open page or folder otherwise.
  const spaceId = routeSpaceId ?? page?.spaceId ?? folder?.spaceId ?? null
  const { data: spaces, error: spacesError } = useSpaces(true)
  const space = spaces?.find((candidate) => candidate.id === spaceId)
  const updateMutation = useUpdatePage()
  const createMutation = useCreatePage()
  const createFolder = useCreateFolder()
  const uploadMedia = useUploadMedia()

  // Mentions name every page, archived included: archiving hides a page from the sidebar but does
  // not break links to it.
  const { data: allPages, error: treeError } = usePageTree(true)

  // The open space as the sidebar shows it, for the breadcrumb and an opened folder's contents.
  const spaceTree = buildSpaceTree(
    allFolders.filter((candidate) => candidate.spaceId === spaceId),
    (allPages ?? []).filter((candidate) => candidate.spaceId === spaceId),
  )

  // The tags exist module-wide: the header offers them, and clicking one fills the sidebar filter.
  const { tags, error: tagsError } = useTags()

  function filterByTag(tagId: string) {
    setTagIds([tagId])
    if (isMobile) setDrawerOpen(true)
    else if (sidebarClosed) toggleSidebar()
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
    setDraft({ title: page.title, icon: page.icon, blocks: page.content.blocks })
    setMobileEditing(false)
  }

  const handleSave = useCallback(
    async (value: PageDraft) => {
      if (!draftPageId) return
      await updateMutation.mutateAsync({
        id: draftPageId,
        body: {
          title: value.title, icon: value.icon, content: { version: 1, blocks: value.blocks },
          ...(value.assistantRevision && savedAssistantRevisions.current.get(draftPageId) !== value.assistantRevision ? { source: 'assistant' as const } : {}),
        },
      })
      if (value.assistantRevision) savedAssistantRevisions.current.set(draftPageId, value.assistantRevision)
    },
    [updateMutation, draftPageId],
  )

  const { status, schedule, flush, reset } = useAutosave<PageDraft>({ onSave: handleSave })

  const setting = usePageSetting()
  const [shareOpen, setShareOpen] = useState(false)

  // ---------- Comments ----------
  const [commentsOpen, setCommentsOpen] = useState(false)
  const [focusThread, setFocusThread] = useState<string | null>(null)
  // A new thread waits on its first comment; the editor marks its words once it exists.
  const [composer, setComposer] = useState<{ blockId: string; quote: string | null; resolve: (id: string | null) => void } | null>(null)
  const { data: comments = [] } = usePageComments(selectedId)
  const { data: stamps } = useBlockStamps(selectedId)
  const stampsById = useMemo(() => new Map((stamps ?? []).map((stamp) => [stamp.blockId, stamp])), [stamps])
  const commentChange = useCommentChange(selectedId ?? '')
  const openThreads = comments.filter((c) => !c.threadId && !c.resolvedAt)
  const blockIds = useMemo(() => {
    const ids = new Set<string>()
    const collect = (blocks: Block[]) => blocks.forEach((block) => {
      ids.add(block.id)
      if (block.children) collect(block.children)
    })
    collect(draft.blocks)
    return ids
  }, [draft.blocks])

  const handleComment = (target: { blockId: string; quote: string | null }) =>
    new Promise<string | null>((resolve) => setComposer({ ...target, resolve }))

  async function submitComment(body: string) {
    if (!composer || !selectedId) return
    try {
      let id: string | null = null
      await commentChange.mutateAsync(async () => {
        id = (await commentsService.addComment(selectedId, { blockId: composer.blockId, quote: composer.quote, body })).id
      })
      composer.resolve(id)
      setComposer(null)
      setFocusThread(id)
      setCommentsOpen(true)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  function openThread(id: string) {
    setFocusThread(id)
    setCommentsOpen(true)
  }

  function jumpToBlock(blockId: string) {
    const hash = `#block-${blockId}`
    if (window.location.hash === hash) window.dispatchEvent(new HashChangeEvent('hashchange'))
    else window.location.hash = hash
  }

  // The words of open threads are painted, and a block with an open thread of its own is marked at
  // its side; a resolved or deleted thread leaves the text as it was.
  const commentStyle = openThreads
    .map((c) => (c.quote ? `.notes-visual [data-thread="${CSS.escape(c.id)}"]` : `.notes-visual [data-block-id="${CSS.escape(c.blockId)}"]`))
    .reduce(
      (css, selector) => css + `${selector}{${selector.includes('data-thread') ? 'background:var(--notes-comment-bg);border-bottom:2px solid var(--notes-comment-line);cursor:pointer' : 'box-shadow:inset 3px 0 0 var(--notes-comment-line)'}}`,
      '',
    )

  // Opening a page puts it first in the recent ones; a visit is not an edit.
  const viewedId = page?.id
  useEffect(() => {
    if (!viewedId) return
    void pagesService
      .markPageViewed(viewedId)
      .then(() => queryClient.invalidateQueries({ queryKey: noteKeys.recentPages() }))
      .catch(() => undefined)
  }, [viewedId, queryClient])

  async function changeSetting(run: () => Promise<unknown>) {
    try {
      await setting.mutateAsync(run as () => Promise<PageDto>)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  const setAppearance = (appearance: PageAppearance) => page && void changeSetting(() => pagesService.setPageAppearance(page.id, appearance))

  async function handleExport(kind: ExportKind) {
    if (!page) return
    const titleOf = (id: string) => allPages?.find((candidate) => candidate.id === id)?.title
    if (kind === 'pdf') {
      // The print stylesheet leaves only the page; "Save as PDF" is the browser's.
      window.print()
      return
    }
    try {
      await flush()
      if (kind === 'markdown') {
        downloadText(fileNameOf(draft.title, 'md'), `# ${draft.title}\n\n${blocksToMarkdown(draft.blocks, titleOf)}`, 'text/markdown;charset=utf-8')
      } else {
        downloadText(fileNameOf(draft.title, 'html'), await blocksToHtml(draft.title, draft.blocks, titleOf, mediaDataUrl), 'text/html;charset=utf-8')
      }
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.actionError')))
    }
  }

  async function openHistory() {
    try {
      await flush()
      setHistoryOpen(true)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
    }
  }

  async function summarizePage() {
    const pageId = selectedId
    try {
      await flush()
      // A navigation while saving must not open a preview for the page that replaced this one.
      if (editorRef.current?.docId === pageId) editorRef.current.summarizePage()
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
    }
  }

  function handleRestored(restored: PageDto) {
    reset()
    setDraft({ title: restored.title, icon: restored.icon, blocks: restored.content.blocks })
    setEditorGeneration((generation) => generation + 1)
    setHistoryOpen(false)
  }

  // Selecting another page: write pending edits to the page being left, then navigate.
  async function handleSelect(id: string | null) {
    setDrawerOpen(false)
    setInfoOpen(false)
    if (id === selectedId && !selectedFolderId) return
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

  async function handleCreateInSpace(folderId: string | null = null) {
    try {
      const created = await createMutation.mutateAsync({ title: t('notes.untitled'), spaceId, folderId })
      await handleSelect(created.id)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
    }
  }

  // Opening a folder leaves the page: its pending edits are written first.
  async function openFolder(id: string) {
    setDrawerOpen(false)
    setInfoOpen(false)
    try {
      await flush()
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
      return
    }
    reset()
    setDraftPageId(null)
    navigate(`/notes/f/${id}`)
  }

  const openNode = (node: SpaceNode) => (node.kind === 'folder' ? openFolder(node.id) : handleSelect(node.id))

  async function handleCreateFolder(parentId: string | null) {
    try {
      const created = await createFolder.mutateAsync({ name: t('notes.folders.untitled'), parentId, spaceId })
      await openFolder(created.id)
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
    }
  }

  function updateDraft(patch: Partial<PageDraft> | ((current: PageDraft) => Partial<PageDraft>)) {
    setDraft((current) => {
      const next = { ...current, ...(typeof patch === 'function' ? patch(current) : patch) }
      schedule(next)
      return next
    })
  }

  // The editor holds every block but the tags one, which the header edits.
  const handleBlocksChange = (blocks: Block[], source?: 'assistant') =>
    updateDraft((current) => ({
      blocks: [...current.blocks.filter((block) => block.type === 'tags'), ...blocks],
      ...(source === 'assistant' ? { assistantRevision: newBlockId() } : {}),
    }))

  const handleTagsChange = (names: string[]) => updateDraft((current) => ({ blocks: withTags(current.blocks, names) }))

  // A page linked from the text before it exists: made in this space, and the link points at it.
  async function handleCreateFromLink(title: string): Promise<string | null> {
    try {
      return (await createMutation.mutateAsync({ title, spaceId })).id
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
      return null
    }
  }

  // Blocks moved out of this page: appended to the other one, with fresh ids so none clashes there.
  async function handleMoveBlocks(targetId: string, blocks: Block[]): Promise<boolean> {
    const renew = (list: Block[]): Block[] => list.map((block) => ({ ...block, id: newBlockId(), ...(block.children ? { children: renew(block.children) } : {}) }))
    try {
      const target = await getPage(targetId)
      await updateMutation.mutateAsync({
        id: targetId,
        body: { title: target.title, icon: target.icon, content: { version: 1, blocks: [...target.content.blocks, ...renew(blocks)] } },
      })
      message.success(t('notes.blocks.moved', { title: target.title || t('notes.untitled') }))
      return true
    } catch (err) {
      message.error(toErrorMessage(err, t('notes.saveError')))
      return false
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

  // A phone opens a page to read it; editing is a step it takes on purpose.
  // A locked page reads until it is unlocked, on any screen.
  const editable = (!isMobile || mobileEditing) && !page?.isLocked
  const border = `1px solid ${token.colorBorderSecondary}`

  const sidebar = (
    <NotesSidebar
      spaceId={spaceId}
      onSpaceChange={handleSpaceChange}
      selectedId={selectedId}
      onSelect={handleSelect}
      selectedFolderId={selectedFolderId}
      onSelectFolder={(id) => void openFolder(id)}
      onOpenOverview={() => void handleSelect(null)}
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

  // The editor starts from the draft, not the saved page, so a remount carries what is not saved yet.
  const visualEditor = selectedId && (
    <VisualEditor
      key={`${selectedId}-${editorGeneration}`}
      docId={selectedId}
      initialContent={draft.blocks}
      pages={allPages ?? []}
      folders={allFolders}
      onOpenFolder={(id) => void openFolder(id)}
      editable={editable}
      onChange={handleBlocksChange}
      onUpload={handleUpload}
      onOpenPage={handleSelect}
      onCreatePage={handleCreateFromLink}
      onMoveBlocks={handleMoveBlocks}
      onComment={handleComment}
      onOpenThread={openThread}
      blockStamps={stampsById}
      ref={editorRef}
    />
  )

  // Where the page stands: locked (with the way out), verified or due for a check.
  const verificationDue = !!page?.verifiedUntil && new Date(page.verifiedUntil) < new Date()
  const pageBadges = page && (
    <>
      {page.isLocked && (
        <Tag icon={<LockOutlined />} className="!me-0 cursor-pointer" onClick={() => void changeSetting(() => pagesService.setPageLocked(page.id, false))}>
          {t('notes.lock.locked')}
        </Tag>
      )}
      {page.verifiedAt && (
        <Tooltip title={t('notes.verify.at', { when: timeAgo(page.verifiedAt, i18n.language) })}>
          <Tag icon={<SafetyCertificateOutlined />} color={verificationDue ? 'warning' : 'success'} className="!me-0">
            {verificationDue
              ? t('notes.verify.due')
              : page.verifiedUntil
                ? t('notes.verify.until', { date: new Date(page.verifiedUntil).toLocaleDateString(i18n.language) })
                : t('notes.verify.verified')}
          </Tag>
        </Tooltip>
      )}
    </>
  )

  const header = page && (
    <PageHeader
      icon={draft.icon}
      title={draft.title}
      editedAt={page.updatedAt ?? page.createdAt}
      status={statusLabel[status]}
      statusIsError={status === 'error'}
      tags={tagsOf(draft.blocks)}
      knownTags={tags}
      editable={editable}
      onIconChange={(icon) => updateDraft({ icon })}
      onTitleChange={(title) => updateDraft({ title })}
      onTagsChange={handleTagsChange}
      onSelectTag={filterByTag}
      badges={pageBadges}
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
          path={pathTo(spaceTree, selectedFolderId ?? selectedId ?? '')}
          current={
            folder ? { icon: folder.icon, name: folder.name }
              : page && selectedId ? { icon: draft.icon, name: draft.title }
                : null
          }
          compact={isMobile}
          onOpenSpace={() => handleSelect(null)}
          onOpenNode={(node) => void openNode(node)}
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
          ) : null}
          {editable && (
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
          <Tooltip title={t('notes.comments.title')}>
            <Badge count={openThreads.length} size="small" offset={[-4, 4]}>
              <Button
                type={commentsOpen ? 'default' : 'text'}
                icon={<CommentOutlined />}
                aria-label={t('notes.comments.title')}
                onClick={() => {
                  setFocusThread(null)
                  setCommentsOpen(!commentsOpen)
                }}
              />
            </Badge>
          </Tooltip>
          <Tooltip title={t('notes.history.title')}>
            <Button
              type="text"
              icon={<HistoryOutlined />}
              aria-label={t('notes.history.title')}
              onClick={() => void openHistory()}
            />
          </Tooltip>
          <PageMenu
            page={page}
            onAppearance={setAppearance}
            onLocked={(locked) => void changeSetting(() => pagesService.setPageLocked(page.id, locked))}
            onTemplate={(isTemplate) => void changeSetting(async () => {
              await flush()
              return pagesService.setPageTemplate(page.id, isTemplate)
            })}
            onVerify={(days) => void changeSetting(() => pagesService.setPageVerification(page.id, days !== false, days === false ? null : days))}
            onShare={() => setShareOpen(true)}
            onExport={(kind) => void handleExport(kind)}
            onSummarize={editable ? () => void summarizePage() : undefined}
          />
        </>
      )}
    </div>
  )

  let body: ReactNode
  const loadError = pageError ?? spacesError ?? treeError ?? tagsError ?? foldersError
  const wide = !!screens.xl
  if (loadError) {
    body = <Alert type="error" showIcon title={loadError.message} />
  } else if (selectedFolderId) {
    body = folder ? (
      <FolderView
        folder={folder}
        includeArchived={includeArchived}
        tagIds={tagIds}
        node={findNode(spaceTree, folder.id)}
        onOpen={(node) => void openNode(node)}
        onCreatePage={(folderId) => void handleCreateInSpace(folderId)}
        onCreateFolder={(parentId) => void handleCreateFolder(parentId)}
        onDeleted={() => void handleSelect(null)}
      />
    ) : (
      <div className="flex h-full items-center justify-center">
        <Spin />
      </div>
    )
  } else if (!selectedId) {
    body = space ? (
      <SpaceOverview
        space={space}
        onOpenPage={handleSelect}
        onOpenFolder={(id) => void openFolder(id)}
        onCreatePage={() => void handleCreateInSpace()}
        onCreateFolder={() => void handleCreateFolder(null)}
      />
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
  } else {
    // One column at a reading width, scrolling as a whole document; the contents beside it when
    // there is room, above it otherwise.
    const side = wide && headingsOf(draft.blocks).length > 0
    body = (
      <div ref={setScroller} className="notes-print h-full overflow-auto">
        <PageCover cover={page.appearance.cover} editable={editable} onChange={(cover) => setAppearance({ ...page.appearance, cover })} />
        <div className={`mx-auto flex w-full justify-center gap-10 px-5 py-8 md:px-10 md:py-12 ${page.appearance.width === 'wide' ? 'max-w-[1500px]' : 'max-w-[1100px]'}`}>
          <div
            className={`notes-doc notes-font-${page.appearance.font} w-full min-w-0 ${page.appearance.width === 'wide' ? '' : 'max-w-[760px]'} ${page.appearance.smallText ? 'notes-small-text' : ''}`}
          >
            {header}
            {!side && <PageToc blocks={draft.blocks} scroller={scroller} placement="top" />}
            {visualEditor}
            <PageFooter pageId={page.id} pages={allPages ?? []} onOpenPage={handleSelect} />
          </div>
          {side && (
            <aside className="w-56 shrink-0">
              <PageToc blocks={draft.blocks} scroller={scroller} placement="side" />
            </aside>
          )}
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

      {page && (
        <ShareModal
          open={shareOpen}
          publicToken={page.publicToken}
          saving={setting.isPending}
          onChange={(isPublic) => void changeSetting(() => pagesService.setPagePublic(page.id, isPublic))}
          onClose={() => setShareOpen(false)}
        />
      )}

      {selectedId && (
        <CommentsPanel
          key={selectedId}
          pageId={selectedId}
          open={commentsOpen}
          comments={comments}
          focusThread={focusThread}
          blockExists={(id) => blockIds.has(id)}
          onJump={jumpToBlock}
          onClose={() => setCommentsOpen(false)}
        />
      )}
      {composer && (
        <CommentComposer
          quote={composer.quote}
          saving={commentChange.isPending}
          onSubmit={(body) => void submitComment(body)}
          onCancel={() => {
            composer.resolve(null)
            setComposer(null)
          }}
        />
      )}
      {commentStyle && <style>{commentStyle}</style>}

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
