import { useEffect, useImperativeHandle, useMemo, useRef, useState, type Ref } from 'react'
import { useTranslation } from 'react-i18next'
import { EditorContent, ReactNodeViewRenderer, useEditor, type Editor } from '@tiptap/react'
import { BubbleMenu } from '@tiptap/react/menus'
import { DragHandle } from '@tiptap/extension-drag-handle-react'
import { Markdown } from '@tiptap/markdown'
import { Placeholder } from '@tiptap/extensions'
import { Input } from 'antd'
import {
  BoldOutlined,
  CodeOutlined,
  DeleteColumnOutlined,
  DeleteOutlined,
  DeleteRowOutlined,
  HolderOutlined,
  InsertRowBelowOutlined,
  InsertRowRightOutlined,
  ItalicOutlined,
  LinkOutlined,
  StrikethroughOutlined,
} from '@ant-design/icons'
import { embedMarkdown } from '../../lib/media'
import { filterCommands, SLASH_COMMANDS, type SlashCommand } from '../../lib/slashCommands'
import { filterTags, tagSlug, type TagIndex } from '../../lib/tags'
import { filterPages, type PageIndex } from '../../lib/wikilinks'
import { newMarked, serializeMarkdown, visualExtensions } from '../../lib/visualMarkdown'
import type { MediaItemDto, PageSummaryDto, TagDto } from '../../models'
import type { MarkdownEditorHandle } from '../MarkdownEditor'
import { TableSizeModal } from '../TableSizeModal'
import { MediaPickerModal } from '../library/MediaPickerModal'
import { PagePickerModal } from '../PagePickerModal'
import { ImageView, RawBlockView, WikilinkView } from './nodeViews'
import { SuggestionMenu } from './SuggestionMenu'
import { menuTrigger, type MenuBridge, type MenuState } from './suggestions'
import { VisualEditorContext, type VisualEditorContextValue } from './visualContext'

interface VisualEditorProps {
  /** Changing this resets the document (a different page was opened). */
  docId: string
  initialValue: string
  pages: PageSummaryDto[]
  tags: TagDto[]
  pageIndex: PageIndex
  tagIndex: TagIndex
  onChange: (value: string) => void
  /** Uploads a pasted/dropped file to the media library and returns the item to embed. */
  onUpload: (file: File) => Promise<MediaItemDto>
  onOpenPage: (pageId: string) => void
  onCreatePage: (title: string) => void
  onSelectTag: (slug: string) => void
  ref?: Ref<MarkdownEditorHandle>
}

/**
 * Writing without the markdown in sight, Notion-style: blocks from `/`, a handle to drag them, a bar
 * over the selection. The page is still saved as markdown — the code editor shows the same text.
 */
export function VisualEditor({
  docId,
  initialValue,
  pages,
  tags,
  pageIndex,
  tagIndex,
  onChange,
  onUpload,
  onOpenPage,
  onCreatePage,
  onSelectTag,
  ref,
}: VisualEditorProps) {
  const { t } = useTranslation()
  const [tableSizeOpen, setTableSizeOpen] = useState(false)
  const [picking, setPicking] = useState<{ kind: 'image' | 'file' | null } | null>(null)
  const [pagePickerOpen, setPagePickerOpen] = useState(false)
  const [menu, setMenu] = useState<MenuState | null>(null)
  const [active, setActive] = useState(0)
  // The link field of the selection bar, open while a URL is being typed.
  const [linkDraft, setLinkDraft] = useState<string | null>(null)

  // Read through refs by the editor, which is built once per page and must not go stale.
  const latest = useRef({ onChange, onUpload, t, pages, tags, menu, active })
  useEffect(() => {
    latest.current = { onChange, onUpload, t, pages, tags, menu, active }
  })

  const bridge = useMemo<MenuBridge>(
    () => ({
      show: (state) => {
        setMenu(state)
        setActive((current) => (current < state.entries.length ? current : 0))
      },
      hide: () => {
        setMenu(null)
        setActive(0)
      },
      keyDown: (event) => {
        const { menu: open, active: index } = latest.current
        if (!open || open.entries.length === 0) return false
        const count = open.entries.length
        if (event.key === 'ArrowDown') setActive((index + 1) % count)
        else if (event.key === 'ArrowUp') setActive((index - 1 + count) % count)
        else if (event.key === 'Enter' || event.key === 'Tab') open.pick(index)
        else if (event.key === 'Escape') setMenu(null)
        else return false
        return true
      },
    }),
    [],
  )

  // The paste handler is wired before the editor exists; it reaches it through here.
  const editorRef = useRef<Editor | null>(null)

  const editor = useEditor(
    {
      extensions: [
        ...visualExtensions({
          wikilink: ReactNodeViewRenderer(WikilinkView, { as: 'span' }),
          image: ReactNodeViewRenderer(ImageView, { as: 'span' }),
          // Everything inside a raw block — its textarea, a <summary> to fold, a link — is its own;
          // only dragging it is the editor's.
          rawBlock: ReactNodeViewRenderer(RawBlockView, { stopEvent: ({ event }) => !event.type.startsWith('drag') }),
        }),
        Markdown.configure({ marked: newMarked() }),
        Placeholder.configure({ placeholder: t('notes.visual.placeholder') }),
        /* eslint-disable react-hooks/refs -- the menus read `latest` when they open, never while rendering */
        menuTrigger<SlashCommand>(bridge, {
          name: 'slashMenu',
          char: '/',
          items: (query) => filterCommands(SLASH_COMMANDS, query, (command) => latest.current.t(command.labelKey)),
          entry: (command) => ({
            key: command.id,
            label: latest.current.t(command.labelKey),
            hint: command.group === 'callout' ? latest.current.t('notes.slash.calloutGroup') : undefined,
          }),
          apply: (instance, range, command) => runSlashCommand(instance, range, command),
        }),
        menuTrigger<{ target: string; icon?: string; hint?: string }>(bridge, {
          name: 'wikilinkMenu',
          char: '[[',
          allowSpaces: true,
          items: (query) => {
            const found = filterPages(latest.current.pages, query).map((page) => ({
              target: page.title,
              icon: page.icon ?? '📄',
              hint: page.slug,
            }))
            const typed = query.trim()
            // A title nothing has yet still links: the page is made when the link is clicked.
            const exists = found.some((page) => page.target.toLowerCase() === typed.toLowerCase())
            return typed && !exists
              ? [...found, { target: typed, icon: '➕', hint: latest.current.t('notes.visual.newPage') }]
              : found
          },
          entry: (page) => ({ key: `${page.target}:${page.hint ?? ''}`, icon: page.icon, label: page.target, hint: page.hint }),
          apply: (instance, range, page) =>
            instance
              .chain()
              .focus()
              .deleteRange(range)
              .insertContent([{ type: 'wikilink', attrs: { target: page.target } }, { type: 'text', text: ' ' }])
              .run(),
        }),
        menuTrigger<{ slug: string; hint?: string }>(bridge, {
          name: 'tagMenu',
          char: '#',
          items: (query) => {
            const found = filterTags(latest.current.tags, query).map((tag) => ({ slug: tag.slug, hint: tag.name }))
            const typed = tagSlug(query)
            return typed && !found.some((tag) => tag.slug === typed) ? [...found, { slug: typed }] : found
          },
          entry: (tag) => ({ key: tag.slug, label: `#${tag.slug}`, hint: tag.hint }),
          apply: (instance, range, tag) => instance.chain().focus().deleteRange(range).insertContent(`#${tag.slug} `).run(),
        }),
        /* eslint-enable react-hooks/refs */
      ],
      content: initialValue,
      contentType: 'markdown',
      onUpdate: ({ editor: instance }) => {
        if (instance.markdown) latest.current.onChange(serializeMarkdown(instance.markdown, instance.getJSON()))
      },
      editorProps: {
        attributes: { class: 'notes-preview notes-visual' },
        handlePaste: (_view, event) => {
          const files = event.clipboardData?.files
          if (files && files.length > 0) {
            event.preventDefault()
            void uploadAll(files)
            return true
          }
          // Text copied from a .md, or from the code editor, comes in as the blocks it describes.
          const text = event.clipboardData?.getData('text/plain')
          if (text && !event.clipboardData?.getData('text/html') && editorRef.current && !editorRef.current.isActive('codeBlock')) {
            editorRef.current.commands.insertContent(text, { contentType: 'markdown' })
            return true
          }
          return false
        },
        handleDrop: (_view, event) => {
          const files = event.dataTransfer?.files
          if (files && files.length > 0) {
            event.preventDefault()
            void uploadAll(files)
            return true
          }
          return false
        },
      },
    },
    [docId],
  )

  useEffect(() => {
    editorRef.current = editor
  }, [editor])

  useImperativeHandle(ref, () => ({ insertMedia: () => setPicking({ kind: null }) }), [])

  async function uploadAll(files: FileList) {
    for (const file of Array.from(files)) {
      const item = await latest.current.onUpload(file)
      insertMedia(item)
    }
  }

  function insertMedia(item: MediaItemDto) {
    editorRef.current?.chain().focus().insertContent(embedMarkdown(item), { contentType: 'markdown' }).run()
  }

  function runSlashCommand(instance: Editor, range: { from: number; to: number }, command: SlashCommand) {
    const chain = instance.chain().focus().deleteRange(range)
    if (command.kind === 'prompt') {
      chain.run()
      if (command.prompt === 'table') setTableSizeOpen(true)
      else if (command.prompt === 'page') setPagePickerOpen(true)
      else setPicking({ kind: command.prompt })
      return
    }
    switch (command.id) {
      case 'h1':
      case 'h2':
      case 'h3':
        chain.setNode('heading', { level: Number(command.id[1]) }).run()
        return
      case 'bulletList':
        chain.toggleBulletList().run()
        return
      case 'numberedList':
        chain.toggleOrderedList().run()
        return
      case 'todo':
        chain.toggleTaskList().run()
        return
      case 'quote':
        chain.toggleBlockquote().run()
        return
      case 'codeBlock':
        chain.toggleCodeBlock().run()
        return
      case 'divider':
        chain.setHorizontalRule().run()
        return
      default: {
        // A toggle or a callout has no block of its own here: it is markdown, opened on its source.
        const source = command.text.replace(/\n+$/, '')
        chain.insertContent({ type: 'rawBlock', attrs: { source, autoEdit: Math.min(command.cursor, source.length) } }).run()
      }
    }
  }

  function applyLink() {
    if (!editor || linkDraft === null) return
    const href = linkDraft.trim()
    const chain = editor.chain().focus().extendMarkRange('link')
    if (href) chain.setLink({ href }).run()
    else chain.unsetLink().run()
    setLinkDraft(null)
  }

  const context: VisualEditorContextValue = { pageIndex, tagIndex, onOpenPage, onCreatePage, onSelectTag }

  const mark = (name: string, icon: React.ReactNode, labelKey: string, run: () => void) => (
    <button
      type="button"
      className="notes-bubble-button"
      aria-pressed={editor?.isActive(name) ?? false}
      title={t(labelKey)}
      aria-label={t(labelKey)}
      onMouseDown={(event) => {
        event.preventDefault()
        run()
      }}
    >
      {icon}
    </button>
  )

  return (
    <VisualEditorContext.Provider value={context}>
      <div className="notes-visual-host">
        {editor && (
          <>
            <DragHandle editor={editor}>
              <div className="notes-drag-handle" title={t('notes.visual.dragHandle')}>
                <HolderOutlined />
              </div>
            </DragHandle>

            <BubbleMenu
              editor={editor}
              pluginKey="formatMenu"
              className="notes-bubble"
              shouldShow={({ editor: instance, state }) =>
                !state.selection.empty && !instance.isActive('codeBlock') && !instance.isActive('image') && !instance.isActive('rawBlock')
              }
            >
              {linkDraft !== null ? (
                <Input
                  size="small"
                  autoFocus
                  value={linkDraft}
                  placeholder="https://"
                  style={{ width: 240 }}
                  onChange={(event) => setLinkDraft(event.target.value)}
                  onPressEnter={applyLink}
                  onBlur={() => setLinkDraft(null)}
                  onKeyDown={(event) => event.key === 'Escape' && setLinkDraft(null)}
                />
              ) : (
                <>
                  {mark('bold', <BoldOutlined />, 'notes.visual.bold', () => editor.chain().focus().toggleBold().run())}
                  {mark('italic', <ItalicOutlined />, 'notes.visual.italic', () => editor.chain().focus().toggleItalic().run())}
                  {mark('strike', <StrikethroughOutlined />, 'notes.visual.strike', () => editor.chain().focus().toggleStrike().run())}
                  {mark('code', <CodeOutlined />, 'notes.visual.code', () => editor.chain().focus().toggleCode().run())}
                  {mark('link', <LinkOutlined />, 'notes.visual.link', () =>
                    setLinkDraft(String(editor.getAttributes('link').href ?? '')),
                  )}
                </>
              )}
            </BubbleMenu>

            <BubbleMenu
              editor={editor}
              pluginKey="tableMenu"
              className="notes-bubble"
              shouldShow={({ editor: instance, state }) => state.selection.empty && instance.isActive('table')}
            >
              {mark('-', <InsertRowBelowOutlined />, 'notes.visual.addRow', () => editor.chain().focus().addRowAfter().run())}
              {mark('-', <InsertRowRightOutlined />, 'notes.visual.addColumn', () => editor.chain().focus().addColumnAfter().run())}
              {mark('-', <DeleteRowOutlined />, 'notes.visual.deleteRow', () => editor.chain().focus().deleteRow().run())}
              {mark('-', <DeleteColumnOutlined />, 'notes.visual.deleteColumn', () => editor.chain().focus().deleteColumn().run())}
              {mark('-', <DeleteOutlined />, 'notes.visual.deleteTable', () => editor.chain().focus().deleteTable().run())}
            </BubbleMenu>
          </>
        )}

        <EditorContent editor={editor} />
      </div>

      {menu && <SuggestionMenu menu={menu} active={active} onHover={setActive} />}

      <MediaPickerModal
        open={!!picking}
        kind={picking?.kind ?? null}
        onClose={() => {
          setPicking(null)
          editor?.commands.focus()
        }}
        onPick={(item) => {
          setPicking(null)
          insertMedia(item)
        }}
      />
      <PagePickerModal
        open={pagePickerOpen}
        pages={pages}
        onClose={() => {
          setPagePickerOpen(false)
          editor?.commands.focus()
        }}
        onPick={(page) => {
          setPagePickerOpen(false)
          editor
            ?.chain()
            .focus()
            .insertContent([{ type: 'wikilink', attrs: { target: page.title } }, { type: 'text', text: ' ' }])
            .run()
        }}
      />
      {tableSizeOpen && (
        <TableSizeModal
          onCancel={() => {
            setTableSizeOpen(false)
            editor?.commands.focus()
          }}
          onConfirm={(rows, columns) => {
            setTableSizeOpen(false)
            editor?.chain().focus().insertTable({ rows, cols: columns, withHeaderRow: true }).run()
          }}
        />
      )}
    </VisualEditorContext.Provider>
  )
}
