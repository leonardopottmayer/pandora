import { useEffect, useImperativeHandle, useMemo, useRef, useState, type Ref } from 'react'
import { useTranslation } from 'react-i18next'
import { EditorContent, ReactNodeViewRenderer, useEditor, useEditorState, type Editor } from '@tiptap/react'
import { BubbleMenu } from '@tiptap/react/menus'
import { DragHandle } from '@tiptap/extension-drag-handle-react'
import { Placeholder } from '@tiptap/extensions'
import { NodeSelection } from '@tiptap/pm/state'
import { isNodeRangeSelection, NodeRangeSelection } from '@tiptap/extension-node-range'
import type { Node as PmNode } from '@tiptap/pm/model'
import { App, Dropdown, Input, type MenuProps } from 'antd'
import {
  BoldOutlined,
  CodeOutlined,
  CommentOutlined,
  DeleteColumnOutlined,
  DeleteOutlined,
  DeleteRowOutlined,
  FontColorsOutlined,
  HolderOutlined,
  InsertRowBelowOutlined,
  InsertRowRightOutlined,
  ItalicOutlined,
  LinkOutlined,
  StrikethroughOutlined,
  UnderlineOutlined,
} from '@ant-design/icons'
import { toBlocks, toEditorDoc, type Block } from '../../lib/blocks'
import { BLOCK_ALIGNMENTS, BLOCK_COLORS, setBlockStyle, TURN_INTO, turnInto, type TurnInto } from '../../lib/blockStyle'
import { filterCommands, SLASH_COMMANDS, slashLabel, type SlashCommand, type SlashGroup } from '../../lib/slashCommands'
import { filterPages } from '../../lib/filterPages'
import { visualExtensions } from '../../lib/visualSchema'
import type { BlockStampDto, FolderDto, MediaItemDto, PageSummaryDto, TextAction } from '../../models'
import { timeAgo } from '../../lib/timeAgo'
import { TableSizeModal } from '../TableSizeModal'
import { MediaPickerModal } from '../library/MediaPickerModal'
import { PagePickerModal } from '../PagePickerModal'
import { getLinkPreview } from '../../services/links.service'
import { ImageView, MediaFileView, MentionView, PageLinkView, UnknownBlockView, type PageLinkLook } from './nodeViews'
import { BlockView } from './BlockView'
import { SuggestionMenu } from './SuggestionMenu'
import { RichBlockView } from './RichBlockView'
import { ChipView } from './ChipView'
import { useQueryClient } from '@tanstack/react-query'
import { createReminder } from '@/modules/agenda/services/reminders.service'
import { noteKeys } from '../../hooks/queryKeys'
import type { DateChip, ItemChip } from '../../lib/blocks'
import { dateSuggestions, formatChipDate, reminderMoment } from '../../lib/chipDates'
import { filterEmojis, type Emoji } from '../../lib/emojis'
import { ITEM_ICONS, listModuleItems } from '../../services/moduleItems.service'
import { DecisionView, SyncedView, TemplateButtonView } from './AdvancedBlocks'
import { menuTrigger, type MenuBridge, type MenuState } from './suggestions'
import { VisualEditorContext, type VisualEditorContextValue } from './visualContext'
import { AiTextPreview } from './AiTextPreview'
import { AI_EDIT_META, aiTextTarget, applyAiText, type AiTextTarget } from '../../lib/aiText'

export interface VisualEditorHandle {
  docId: string
  /** Opens the media library to put an image or a file where the cursor is. */
  insertMedia: () => void
  summarizePage: () => void
}

interface VisualEditorProps {
  /** Changing this resets the document (a different page was opened). */
  docId: string
  initialContent: Block[]
  pages: PageSummaryDto[]
  /** The space's folders, for the breadcrumb block. */
  folders?: FolderDto[]
  /** Off, the page reads: nothing to type in, links still open. */
  editable?: boolean
  onChange?: (blocks: Block[], source?: 'assistant') => void
  /** Uploads a pasted/dropped file to the media library and returns the item to show. */
  onUpload?: (file: File) => Promise<MediaItemDto>
  onOpenPage: (pageId: string) => void
  onOpenFolder?: (folderId: string) => void
  /** Read through a public link: pages it names show as private. */
  publicView?: boolean
  /** Creates a page with the title typed after `@` or `[[`, for a link to it; null when it failed. */
  onCreatePage?: (title: string) => Promise<string | null>
  /** Puts blocks at the end of another page; true once they are there, and they leave this one. */
  onMoveBlocks?: (pageId: string, blocks: Block[]) => Promise<boolean>
  /** Starts a comment thread on a block, or on the words `quote` of it; answers the thread's id, or null. */
  onComment?: (target: { blockId: string; quote: string | null }) => Promise<string | null>
  /** Commented words were clicked. */
  onOpenThread?: (threadId: string) => void
  /** When each block was written and changed, shown in its menu. */
  blockStamps?: Map<string, BlockStampDto>
  ref?: Ref<VisualEditorHandle>
}

/** The prefix of a block's address in the page URL: `/notes/<page>#block-<id>`. */
export const BLOCK_ANCHOR = 'block-'

/** A node view's wrapper carries what the node renders (`data-block-id`, colours), as plain nodes do. */
const carry = {
  attrs: ({ HTMLAttributes }: { HTMLAttributes: Record<string, unknown> }) =>
    Object.fromEntries(Object.entries(HTMLAttributes).filter(([, value]) => value != null && value !== '').map(([key, value]) => [key, String(value)])),
}

interface PageEntry {
  pageId?: string
  title: string
  icon: string
  hint?: string
  /** After `@`, a date or another module's item instead of a page. */
  chip?: { kind: 'date'; data: DateChip; reminder: boolean } | { kind: 'item'; data: ItemChip }
  section?: string
}

/** Notion-style writing: blocks from `/`, a handle to drag them, a bar over the selection. */
export function VisualEditor({
  docId,
  initialContent,
  pages,
  folders,
  editable = true,
  onChange,
  onUpload,
  onOpenPage,
  onOpenFolder,
  publicView,
  onCreatePage,
  onMoveBlocks,
  onComment,
  onOpenThread,
  blockStamps,
  ref,
}: VisualEditorProps) {
  const { t, i18n } = useTranslation()
  const { message } = App.useApp()
  const [tableSizeOpen, setTableSizeOpen] = useState(false)
  const [picking, setPicking] = useState<{ kind: 'image' | 'file' | null } | null>(null)
  // A block that asked the library for an item (a gallery, an audio, a PDF) waits on this.
  const pickResolver = useRef<((item: MediaItemDto | null) => void) | null>(null)
  // A link pasted alone on a line, while the menu asks what it should become.
  const pasteOffer = useRef<{ pos: number; url: string } | null>(null)
  // The page picker, and what the picked page becomes: a link in the text, a card or a preview
  // block — or, for `move`, where the selected blocks go.
  const [pagePicker, setPagePicker] = useState<PageLinkLook | 'move' | null>(null)
  // The block under the drag handle, which its menu acts on.
  const handleTarget = useRef<{ node: PmNode; pos: number } | null>(null)
  const [menu, setMenu] = useState<MenuState | null>(null)
  const [active, setActive] = useState(0)
  // The link field of the selection bar, open while a URL is being typed.
  const [linkDraft, setLinkDraft] = useState<string | null>(null)
  // The block whose menu is open, for its dates.
  const [menuBlockId, setMenuBlockId] = useState<string | null>(null)
  const [aiTarget, setAiTarget] = useState<AiTextTarget | null>(null)

  // Read through refs by the editor, which is built once per page and must not go stale.
  const queryClient = useQueryClient()
  const latest = useRef({ onChange, onUpload, onCreatePage, t, pages, menu, active })
  useEffect(() => {
    latest.current = { onChange, onUpload, onCreatePage, t, pages, menu, active }
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

  // What the page holds as far as the parent knows, to tell a real change from the editor's own housekeeping.
  const sent = useRef(JSON.stringify(initialContent.filter((block) => block.type !== 'tags')))

  // The paste handler is wired before the editor exists; it reaches it through here.
  const editorRef = useRef<Editor | null>(null)

  /* eslint-disable react-hooks/refs -- the menus read `latest` when they open, never while rendering */
  /** Pages for `[[`; for `@` also dates (with or without a reminder) and other modules' items. */
  const pageMenu = (name: string, char: string, chips: boolean) =>
    menuTrigger<PageEntry>(bridge, {
      name,
      char,
      allowSpaces: true,
      items: async (query) => {
        const { t: tr } = latest.current
        const pagesSection = chips ? tr('notes.chips.sections.pages') : undefined
        const found: PageEntry[] = filterPages(latest.current.pages.filter((page) => page.id !== docId), query).map((page) => ({
          pageId: page.id,
          title: page.title || tr('notes.untitled'),
          icon: page.icon ?? '📄',
          hint: page.slug,
          section: pagesSection,
        }))
        const typed = query.trim()
        // A title no page has yet: the page is made, and the link points at it.
        const exists = found.some((page) => page.title.toLowerCase() === typed.toLowerCase())
        const pagesFound = typed && !exists && latest.current.onCreatePage
          ? [...found.slice(0, 8), { title: typed, icon: '➕', hint: tr('notes.visual.newPage'), section: pagesSection }]
          : found.slice(0, 8)
        if (!chips) return pagesFound

        const words = (word: 'today' | 'tomorrow' | 'yesterday') => tr(`notes.chips.${word}`)
        const dates: PageEntry[] = dateSuggestions(query).map((date) => ({
          title: (date.reminder ? `${tr('notes.chips.remind')}: ` : '') + formatChipDate(date.value, i18n.language, words),
          icon: date.reminder ? '⏰' : '📅',
          section: tr('notes.chips.sections.dates'),
          chip: { kind: 'date', data: { value: date.value }, reminder: date.reminder },
        }))
        let items: ItemChip[] = []
        if (typed) {
          items = await queryClient
            .fetchQuery({ queryKey: noteKeys.moduleItems(), queryFn: listModuleItems, staleTime: 60_000 })
            .catch(() => [])
        }
        const term = typed.toLowerCase()
        const others: PageEntry[] = items
          .filter((item) => item.label.toLowerCase().includes(term))
          .slice(0, 6)
          .map((item) => ({
            title: item.label,
            icon: ITEM_ICONS[item.kind] ?? '🔗',
            hint: tr(`notes.chips.kinds.${item.kind}`),
            section: tr('notes.chips.sections.items'),
            chip: { kind: 'item', data: item },
          }))
        return [...pagesFound, ...dates, ...others]
      },
      entry: (page) => ({
        key: page.chip ? `${page.chip.kind}:${page.chip.kind === 'date' ? page.chip.data.value + page.chip.reminder : page.chip.data.id}` : (page.pageId ?? `new:${page.title}`),
        icon: page.icon,
        label: page.title,
        hint: page.hint,
        section: page.section,
      }),
      apply: (instance, range, page) => {
        // The line around the `@`, for the name of a reminder made from it.
        const $at = instance.state.doc.resolve(range.from)
        const line = ($at.parent.textBetween(0, $at.parentOffset, ' ') + ' ' + $at.parent.textBetween(Math.min($at.parentOffset + (range.to - range.from), $at.parent.content.size), $at.parent.content.size, ' ')).replace(/\s+/g, ' ').trim()
        instance.chain().focus().deleteRange(range).run()
        const insert = (node: Record<string, unknown>) => instance.chain().focus().insertContent([node, { type: 'text', text: ' ' }]).run()
        const chip = page.chip
        if (chip?.kind === 'date' && chip.reminder) {
          const title = line || latest.current.pages.find((candidate) => candidate.id === docId)?.title || latest.current.t('notes.untitled')
          void createReminder({ title, remindAt: reminderMoment(chip.data.value), timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone })
            .then((reminder) => insert({ type: 'chip', attrs: { kind: 'date', data: { value: chip.data.value, reminderId: reminder.id } } }))
            .catch(() => {
              message.error(latest.current.t('notes.chips.reminderError'))
              insert({ type: 'chip', attrs: { kind: 'date', data: chip.data } })
            })
        } else if (chip) insert({ type: 'chip', attrs: { kind: chip.kind, data: chip.data } })
        else if (page.pageId) insert({ type: 'mention', attrs: { pageId: page.pageId } })
        else void latest.current.onCreatePage?.(page.title).then((id) => id && insert({ type: 'mention', attrs: { pageId: id } }))
      },
    })

  const groupLabel = (group: SlashGroup) => latest.current.t(`notes.slash.groups.${group}`)

  const editor = useEditor(
    {
      editable,
      extensions: [
        ...visualExtensions({
          mention: ReactNodeViewRenderer(MentionView, { as: 'span' }),
          chip: ReactNodeViewRenderer(ChipView, { as: 'span' }),
          image: ReactNodeViewRenderer(ImageView, carry),
          mediaFile: ReactNodeViewRenderer(MediaFileView, carry),
          unknownBlock: ReactNodeViewRenderer(UnknownBlockView, carry),
          callout: ReactNodeViewRenderer(BlockView, carry),
          details: ReactNodeViewRenderer(BlockView, carry),
          pageLink: ReactNodeViewRenderer(PageLinkView, carry),
          richBlock: ReactNodeViewRenderer(RichBlockView, carry),
          synced: ReactNodeViewRenderer(SyncedView, carry),
          templateButton: ReactNodeViewRenderer(TemplateButtonView, carry),
          decision: ReactNodeViewRenderer(DecisionView, carry),
        }),
        Placeholder.configure({ placeholder: t('notes.visual.placeholder') }),
        menuTrigger<SlashCommand>(bridge, {
          name: 'slashMenu',
          char: '/',
          items: (query) =>
            filterCommands(SLASH_COMMANDS, query, (command) => slashLabel(latest.current.t, command), (group) => groupLabel(group)),
          entry: (command) => ({
            key: command.id,
            label: slashLabel(latest.current.t, command),
            section: groupLabel(command.group),
          }),
          apply: (instance, range, command) => runSlashCommand(instance, range, command),
        }),
        pageMenu('pageMenu', '[[', false),
        pageMenu('mentionMenu', '@', true),
        menuTrigger<Emoji>(bridge, {
          name: 'emojiMenu',
          char: ':',
          items: (query) => filterEmojis(query),
          entry: (emoji) => ({ key: emoji.char, icon: emoji.char, label: emoji.words[0] }),
          apply: (instance, range, emoji) => instance.chain().focus().deleteRange(range).insertContent(emoji.char).run(),
        }),
      ],
      content: toEditorDoc(initialContent),
      onUpdate: ({ editor: instance, transaction }) => {
        const blocks = toBlocks(instance.getJSON().content)
        // The empty paragraph the editor keeps at the end, to have somewhere to type after a block,
        // is not part of the page.
        const last = blocks.at(-1)
        if (last?.type === 'paragraph' && !last.content && !last.children) blocks.pop()
        // Selecting a block can add that paragraph, which changes nothing worth saving.
        const json = JSON.stringify(blocks)
        if (json === sent.current) return
        sent.current = json
        if (transaction.getMeta(AI_EDIT_META)) latest.current.onChange?.(blocks, 'assistant')
        else latest.current.onChange?.(blocks)
      },
      editorProps: {
        attributes: { class: 'notes-preview notes-visual' },
        handlePaste: (view, event) => {
          const files = event.clipboardData?.files
          if (files && files.length > 0 && latest.current.onUpload) {
            event.preventDefault()
            void uploadAll(files)
            return true
          }
          // A link alone on an empty line: written as a link, and a menu offers a card or an embed.
          const text = event.clipboardData?.getData('text/plain').trim() ?? ''
          const { $from, empty } = view.state.selection
          if (/^https?:\/\/\S+$/.test(text) && empty && $from.parent.type.name === 'paragraph' && $from.parent.content.size === 0) {
            event.preventDefault()
            const pos = $from.before()
            editorRef.current?.chain().insertContent({ type: 'text', text, marks: [{ type: 'link', attrs: { href: text } }] }).run()
            offerPaste(pos, text)
            return true
          }
          return false
        },
        handleKeyDown: (_view, event) => {
          if (!pasteOffer.current) return false
          if (bridge.keyDown(event)) return true
          closePasteOffer()
          return false
        },
        handleClick: () => {
          if (pasteOffer.current) closePasteOffer()
          return false
        },
        handleDrop: (_view, event) => {
          const files = event.dataTransfer?.files
          if (files && files.length > 0 && latest.current.onUpload) {
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
  /* eslint-enable react-hooks/refs */

  useEffect(() => {
    editorRef.current = editor
  }, [editor])

  // How many blocks a block selection holds, for the bar over it.
  const selectedBlocks = useEditorState({
    editor,
    selector: ({ editor: instance }) => (instance && isNodeRangeSelection(instance.state.selection) ? instance.state.selection.ranges.length : 0),
  })

  // A link to a block (`#block-<id>`) brings it into view, marked for a moment.
  useEffect(() => {
    if (!editor) return
    const reveal = () => {
      const hash = window.location.hash
      if (!hash.startsWith(`#${BLOCK_ANCHOR}`)) return
      const id = CSS.escape(hash.slice(1 + BLOCK_ANCHOR.length))
      const element = editor.view.dom.querySelector(`[data-block-id="${id}"]`)
      if (!element) return
      element.scrollIntoView?.({ block: 'center' })
      element.classList.add('notes-block-target')
      window.setTimeout(() => element.classList.remove('notes-block-target'), 2000)
    }
    // After the node views have drawn their wrappers.
    const timer = window.setTimeout(reveal, 0)
    window.addEventListener('hashchange', reveal)
    return () => {
      window.clearTimeout(timer)
      window.removeEventListener('hashchange', reveal)
    }
  }, [editor])

  // Without `false` TipTap reports an update, and an opened page would be saved.
  useEffect(() => {
    if (editor && editor.isEditable !== editable) editor.setEditable(editable, false)
  }, [editor, editable])

  useImperativeHandle(ref, () => ({
    docId,
    insertMedia: () => setPicking({ kind: null }),
    summarizePage: () => {
      const instance = editorRef.current
      if (instance?.isEditable) setAiTarget({ ...aiTextTarget(instance, 'summarize'), pageSummary: true })
    },
  }), [docId])

  async function uploadAll(files: FileList) {
    for (const file of Array.from(files)) {
      const item = await latest.current.onUpload!(file)
      insertMedia(item)
    }
  }

  /** An item of the library as the block it reads best as: an image, a player, a PDF reader, a file card. */
  function insertMedia(item: MediaItemDto) {
    const type = item.contentType.toLowerCase()
    const node = item.kind === 'image'
      ? { type: 'image', attrs: { mediaId: item.id } }
      : type.startsWith('audio/')
        ? { type: 'richBlock', attrs: { kind: 'audio', props: { mediaId: item.id, name: item.name } } }
        : type === 'application/pdf'
          ? { type: 'richBlock', attrs: { kind: 'pdf', props: { mediaId: item.id, name: item.name } } }
          : { type: 'mediaFile', attrs: { mediaId: item.id, name: item.name } }
    editorRef.current?.chain().focus().insertContent(node).run()
  }

  function pickMedia(kind: 'image' | 'file') {
    pickResolver.current?.(null)
    return new Promise<MediaItemDto | null>((resolve) => {
      pickResolver.current = resolve
      setPicking({ kind })
    })
  }

  function closePasteOffer() {
    pasteOffer.current = null
    bridge.hide()
  }

  function offerPaste(pos: number, url: string) {
    const instance = editorRef.current
    if (!instance) return
    pasteOffer.current = { pos, url }
    const coords = instance.view.coordsAtPos(instance.state.selection.from)
    const choices = ['link', 'bookmark', 'embed'] as const
    bridge.show({
      entries: choices.map((choice) => ({ key: choice, label: t(`notes.rich.pasteAs.${choice}`), section: t('notes.rich.pasteAs.title') })),
      rect: new DOMRect(coords.left, coords.top, 0, coords.bottom - coords.top),
      pick: (index) => {
        const offer = pasteOffer.current
        closePasteOffer()
        const choice = choices[index]
        const line = offer && instance.state.doc.nodeAt(offer.pos)
        if (!offer || choice === 'link' || line?.type.name !== 'paragraph' || line.textContent.trim() !== offer.url) return
        instance
          .chain()
          .focus()
          .insertContentAt({ from: offer.pos, to: offer.pos + line.nodeSize }, { type: 'richBlock', attrs: { kind: choice, props: { url: offer.url } } })
          .run()
        if (choice === 'bookmark') void fillBookmark(offer.url)
      },
    })
  }

  /** A bookmark made from a paste: its card filled in once the page has been read. */
  async function fillBookmark(url: string) {
    try {
      const preview = await getLinkPreview(url)
      const instance = editorRef.current
      if (!instance) return
      instance.state.doc.descendants((node, pos) => {
        if (node.type.name !== 'richBlock' || node.attrs.kind !== 'bookmark' || node.attrs.props?.url !== url || node.attrs.props?.title) return
        const props = Object.fromEntries(
          Object.entries({ url, title: preview.title, description: preview.description, siteName: preview.siteName, imageUrl: preview.imageUrl, iconUrl: preview.iconUrl })
            .filter(([, value]) => value != null && value !== ''),
        )
        instance.view.dispatch(instance.state.tr.setNodeMarkup(pos, undefined, { ...node.attrs, props }))
      })
    } catch {
      // A page that cannot be read stays a card with its address.
    }
  }

  function runSlashCommand(instance: Editor, range: { from: number; to: number }, command: SlashCommand) {
    const chain = instance.chain().focus().deleteRange(range)
    if (command.prompt) {
      chain.run()
      if (command.prompt === 'table') setTableSizeOpen(true)
      else if (command.prompt === 'page') setPagePicker(command.id === 'pageCard' ? 'card' : command.id === 'pagePreview' ? 'preview' : 'inline')
      else setPicking({ kind: command.prompt })
      return
    }
    // A change to the block the slash was typed in.
    const [verb, value] = [command.id.slice(0, command.id.indexOf('-')), command.id.slice(command.id.indexOf('-') + 1)]
    if (verb === 'color' || verb === 'background') {
      chain.run()
      setBlockStyle(instance, verb, value === 'default' ? null : value)
      return
    }
    if (verb === 'turn') {
      chain.run()
      turnInto(instance, value as TurnInto)
      return
    }
    switch (command.id) {
      case 'aiContinue':
        chain.run()
        setAiTarget(aiTextTarget(instance, 'continue'))
        return
      case 'paragraph':
        chain.run()
        turnInto(instance, 'paragraph')
        return
      case 'date':
        chain.insertContent([{ type: 'chip', attrs: { kind: 'date', data: { value: dateSuggestions('')[0].value } } }, { type: 'text', text: ' ' }]).run()
        return
      case 'status':
        chain.insertContent([{ type: 'chip', attrs: { kind: 'status', data: { label: t('notes.chips.newStatus'), color: 'blue' } } }, { type: 'text', text: ' ' }]).run()
        return
      case 'inlineMath':
        chain.insertContent([{ type: 'chip', attrs: { kind: 'math', data: { latex: 'x^2' } } }, { type: 'text', text: ' ' }]).run()
        return
      case 'mermaid':
      case 'ai':
      case 'math':
      case 'breadcrumb':
      case 'subpages':
      case 'tag-pages':
      case 'properties':
      case 'agenda-list':
      case 'finance-summary':
      case 'gallery':
      case 'video':
      case 'audio':
      case 'pdf':
      case 'bookmark':
      case 'embed':
        chain.insertContent({ type: 'richBlock', attrs: { kind: command.id, props: {} } }).run()
        return
      case 'synced':
        chain.insertContent({ type: 'synced', content: [{ type: 'paragraph' }] }).run()
        return
      case 'templateButton':
        chain.insertContent({ type: 'templateButton', attrs: { editing: true }, content: [{ type: 'paragraph' }] }).run()
        return
      case 'decision':
        chain.setNode('decision').run()
        return
      case 'columns2':
      case 'columns3':
      case 'columns4':
        chain.insertContent({
          type: 'columns',
          content: Array.from({ length: Number(command.id.at(-1)) }, () => ({ type: 'column', content: [{ type: 'paragraph' }] })),
        }).run()
        return
      case 'toggleH1':
      case 'toggleH2':
      case 'toggleH3':
        chain.insertContent({
          type: 'details',
          attrs: { level: Number(command.id.at(-1)), open: true, autoFocus: true },
          content: [{ type: 'paragraph' }],
        }).run()
        return
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
        const toggle = command.id === 'toggle'
        chain.insertContent({
          type: toggle ? 'details' : 'callout',
          attrs: toggle ? { open: true, autoFocus: true } : { calloutType: command.id.replace('callout-', ''), autoFocus: true },
          content: [{ type: 'paragraph' }],
        }).run()
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

  // ---------- Block actions: the drag handle's menu, and the bar over a selection of blocks ----------

  /** On a click of the handle: its block becomes the selection, or with Shift joins it. */
  function selectHandleTarget(extend: boolean) {
    const target = handleTarget.current
    if (!editor || !target) return
    const { state } = editor
    const end = target.pos + target.node.nodeSize
    const selection = extend
      ? NodeRangeSelection.create(state.doc, Math.min(state.selection.from, target.pos), Math.max(state.selection.to, end))
      : NodeSelection.create(state.doc, target.pos)
    editor.view.dispatch(state.tr.setSelection(selection))
  }

  /** The id of the one selected block, for a link to it. */
  function selectedBlockId(): string | null {
    const selection = editor?.state.selection
    if (!(selection instanceof NodeSelection)) return null
    const node = selection.node
    return (node.attrs.blockId as string | null) ?? (node.firstChild?.attrs.blockId as string | null) ?? null
  }

  /** The stored block the position is in: the innermost node that is a block of the page, not the head line of a list item or a table cell. */
  function blockIdAt(pos: number): string | null {
    if (!editor) return null
    const stored = new Set<string>()
    const collect = (blocks: Block[]) => blocks.forEach((block) => {
      stored.add(block.id)
      if (block.children) collect(block.children)
    })
    collect(toBlocks(editor.getJSON().content ?? []))
    const $pos = editor.state.doc.resolve(pos)
    for (let depth = $pos.depth; depth > 0; depth--) {
      const id = $pos.node(depth).attrs.blockId as string | null
      if (id && stored.has(id)) return id
    }
    return null
  }

  async function commentOnSelection() {
    if (!editor || !onComment) return
    const { from, to } = editor.state.selection
    const blockId = blockIdAt(from)
    if (!blockId) return
    const thread = await onComment({ blockId, quote: editor.state.doc.textBetween(from, to, ' ').slice(0, 500) })
    if (thread) editor.chain().focus().setTextSelection({ from, to }).setMark('comment', { thread }).run()
  }

  async function commentOnBlock() {
    const blockId = selectedBlockId()
    if (blockId && onComment) await onComment({ blockId, quote: null })
  }

  /** When the block was written and changed, as one line of its menu. */
  function datesLabel(blockId: string | null): string | null {
    const stamp = blockId ? blockStamps?.get(blockId) : undefined
    if (!stamp) return null
    const created = timeAgo(stamp.createdAt, i18n.language)
    if (stamp.updatedAt === stamp.createdAt) return t('notes.comments.written', { when: created })
    const by = stamp.updatedSource === 'assistant' ? t('notes.comments.byAssistant') : stamp.updatedSource === 'restore' ? t('notes.comments.byRestore') : ''
    return t('notes.comments.writtenEdited', { created, edited: timeAgo(stamp.updatedAt, i18n.language), by })
  }

  async function copyBlockLink() {
    const id = selectedBlockId()
    if (!id) return
    const url = `${window.location.origin}/notes/${docId}#${BLOCK_ANCHOR}${id}`
    try {
      await navigator.clipboard.writeText(url)
      message.success(t('notes.blocks.linkCopied'))
    } catch {
      message.error(t('notes.blocks.copyError'))
    }
  }

  function duplicateBlocks() {
    if (!editor) return
    const { state } = editor
    const { content } = state.selection.content()
    // The copies get fresh ids from BlockIds.
    editor.view.dispatch(state.tr.insert(state.selection.to, content))
  }

  async function moveBlocks(pageId: string) {
    if (!editor || !onMoveBlocks) return
    const { selection } = editor.state
    const blocks = toBlocks(selection.content().content.toJSON() as Parameters<typeof toBlocks>[0])
    if (!blocks.length) return
    if (await onMoveBlocks(pageId, blocks)) editor.chain().focus().deleteRange({ from: selection.from, to: selection.to }).run()
  }

  const swatch = (kind: 'color' | 'background', color: string) => (
    <span className={`notes-swatch notes-swatch-${kind}`} data-swatch={color} aria-hidden="true">{kind === 'color' ? 'A' : ''}</span>
  )

  function blockMenuItems(single: boolean): MenuProps['items'] {
    if (!editor) return []
    const colors = (kind: 'color' | 'background') =>
      ['default', ...BLOCK_COLORS].map((color) => ({
        key: `${kind}-${color}`,
        icon: swatch(kind, color),
        label: t(kind === 'color' ? 'notes.slash.textColor' : 'notes.slash.backgroundColor', { color: t(`notes.colors.${color}`) }),
        onClick: () => setBlockStyle(editor, kind, color === 'default' ? null : color),
      }))
    const dates = single ? datesLabel(menuBlockId) : null
    return [
      ...(dates ? [{ key: 'dates', disabled: true, label: <span className="text-xs">{dates}</span> }, { type: 'divider' as const }] : []),
      {
        key: 'turn',
        label: t('notes.blocks.turnInto'),
        children: TURN_INTO.map((kind) => ({ key: `turn-${kind}`, label: t(`notes.slash.${kind}`), onClick: () => turnInto(editor, kind) })),
      },
      {
        key: 'color',
        label: t('notes.blocks.color'),
        children: [
          { type: 'group', key: 'text', label: t('notes.blocks.textColor'), children: colors('color') },
          { type: 'group', key: 'background', label: t('notes.blocks.background'), children: colors('background') },
        ],
      },
      {
        key: 'align',
        label: t('notes.blocks.align'),
        children: BLOCK_ALIGNMENTS.map((align) => ({
          key: `align-${align}`,
          label: t(`notes.blocks.aligns.${align}`),
          onClick: () => setBlockStyle(editor, 'align', align === 'left' ? null : align),
        })),
      },
      { type: 'divider' },
      ...(single && onComment ? [{ key: 'comment', label: t('notes.comments.comment'), onClick: () => void commentOnBlock() }] : []),
      ...(single ? [{ key: 'link', label: t('notes.blocks.copyLink'), onClick: () => void copyBlockLink() }] : []),
      { key: 'duplicate', label: t('notes.blocks.duplicate'), onClick: duplicateBlocks },
      ...(onMoveBlocks ? [{ key: 'move', label: t('notes.blocks.moveTo'), onClick: () => setPagePicker('move') }] : []),
      { type: 'divider' },
      { key: 'delete', danger: true, label: t('common.delete'), onClick: () => editor.chain().focus().deleteSelection().run() },
    ]
  }

  /** Colours for the selected text: its own (`color`) or behind it (`highlight`). */
  const textColorItems = (): MenuProps['items'] => {
    if (!editor) return []
    const colors = (mark: 'color' | 'highlight', kind: 'color' | 'background') =>
      ['default', ...BLOCK_COLORS].map((color) => ({
        key: `${mark}-${color}`,
        icon: swatch(kind, color),
        label: t(kind === 'color' ? 'notes.slash.textColor' : 'notes.slash.backgroundColor', { color: t(`notes.colors.${color}`) }),
        onClick: () =>
          color === 'default' ? editor.chain().focus().unsetMark(mark).run() : editor.chain().focus().setMark(mark, { color }).run(),
      }))
    return [
      { type: 'group', key: 'text', label: t('notes.blocks.textColor'), children: colors('color', 'color') },
      { type: 'group', key: 'background', label: t('notes.blocks.background'), children: colors('highlight', 'background') },
    ]
  }

  /** A page link in the text made a block below its line: a card or a preview. */
  function mentionToBlock(look: 'card' | 'preview') {
    if (!editor) return
    const selection = editor.state.selection
    if (!(selection instanceof NodeSelection) || selection.node.type.name !== 'mention') return
    const pageId = selection.node.attrs.pageId as string
    const { $from } = selection
    const line = $from.parent
    let mentions = 0
    line.forEach((child) => {
      if (child.type.name === 'mention') mentions++
    })
    const block = { type: 'pageLink', attrs: { pageId, look } }
    // Alone on its line, the link takes the line's place; otherwise the line keeps its text.
    if (mentions === 1 && line.textContent.trim() === '') {
      editor.chain().focus().insertContentAt({ from: $from.before(), to: $from.after() }, block).run()
    } else {
      editor.chain().focus().insertContentAt($from.after(), block).deleteRange({ from: selection.from, to: selection.to }).run()
    }
  }

  function insertPage(pageId: string, look: PageLinkLook) {
    const chain = editor?.chain().focus()
    if (look === 'inline') chain?.insertContent([{ type: 'mention', attrs: { pageId } }, { type: 'text', text: ' ' }]).run()
    else chain?.insertContent({ type: 'pageLink', attrs: { pageId, look } }).run()
  }

  const pagesById = useMemo(() => new Map(pages.map((page) => [page.id, page])), [pages])
  const foldersById = useMemo(() => new Map((folders ?? []).map((folder) => [folder.id, folder])), [folders])
  const context: VisualEditorContextValue = { pageId: docId, editable, pagesById, foldersById, onOpenPage, onOpenFolder, publicView, pickMedia }

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
        {editor && editable && (
          <>
            <DragHandle editor={editor} onNodeChange={({ node, pos }) => (handleTarget.current = node ? { node, pos } : null)}>
              <Dropdown
                trigger={['click']}
                destroyOnHidden
                menu={{ items: blockMenuItems(true) }}
                onOpenChange={(open) => {
                  const node = handleTarget.current?.node
                  if (open) setMenuBlockId((node?.attrs.blockId as string | null) ?? (node?.firstChild?.attrs.blockId as string | null) ?? null)
                }}
              >
                <div
                  className="notes-drag-handle"
                  role="button"
                  tabIndex={-1}
                  title={t('notes.visual.dragHandle')}
                  aria-label={t('notes.visual.dragHandle')}
                  onClick={(event) => selectHandleTarget(event.shiftKey)}
                >
                  <HolderOutlined />
                </div>
              </Dropdown>
            </DragHandle>

            <BubbleMenu
              editor={editor}
              pluginKey="formatMenu"
              className="notes-bubble"
              shouldShow={({ editor: instance, state }) =>
                !state.selection.empty &&
                !(state.selection instanceof NodeSelection) &&
                !isNodeRangeSelection(state.selection) &&
                !instance.isActive('codeBlock')
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
                  {mark('underline', <UnderlineOutlined />, 'notes.visual.underline', () => editor.chain().focus().toggleUnderline().run())}
                  {mark('strike', <StrikethroughOutlined />, 'notes.visual.strike', () => editor.chain().focus().toggleStrike().run())}
                  {mark('code', <CodeOutlined />, 'notes.visual.code', () => editor.chain().focus().toggleCode().run())}
                  {mark('link', <LinkOutlined />, 'notes.visual.link', () =>
                    setLinkDraft(String(editor.getAttributes('link').href ?? '')),
                  )}
                  {onComment && mark('-', <CommentOutlined />, 'notes.comments.comment', () => void commentOnSelection())}
                  <Dropdown
                    trigger={['click']}
                    destroyOnHidden
                    menu={{ items: (['summarize', 'improve', 'translate'] as TextAction[]).map((action) => ({
                      key: action, label: t(`notes.ai.${action}`), onClick: () => setAiTarget(aiTextTarget(editor, action)),
                    })) }}
                  >
                    <button type="button" className="notes-bubble-button" aria-label={t('notes.ai.menu')} onMouseDown={(event) => event.preventDefault()}>
                      {t('notes.ai.menu')} ▾
                    </button>
                  </Dropdown>
                  <Dropdown trigger={['click']} destroyOnHidden menu={{ items: textColorItems() }}>
                    <button
                      type="button"
                      className="notes-bubble-button"
                      title={t('notes.blocks.color')}
                      aria-label={t('notes.blocks.color')}
                      onMouseDown={(event) => event.preventDefault()}
                    >
                      <FontColorsOutlined />
                    </button>
                  </Dropdown>
                </>
              )}
            </BubbleMenu>

            <BubbleMenu
              editor={editor}
              pluginKey="blocksMenu"
              className="notes-bubble"
              shouldShow={({ state }) => isNodeRangeSelection(state.selection)}
            >
              <Dropdown trigger={['click']} destroyOnHidden menu={{ items: blockMenuItems(false) }}>
                <button type="button" className="notes-bubble-button" onMouseDown={(event) => event.preventDefault()}>
                  {t('notes.blocks.selected', { count: selectedBlocks ?? 0 })} ▾
                </button>
              </Dropdown>
              {mark('-', <DeleteOutlined />, 'common.delete', () => editor.chain().focus().deleteSelection().run())}
            </BubbleMenu>

            <BubbleMenu
              editor={editor}
              pluginKey="mentionMenu"
              className="notes-bubble"
              shouldShow={({ state }) => state.selection instanceof NodeSelection && state.selection.node.type.name === 'mention'}
            >
              {mark('-', <span>{t('notes.visual.look.card')}</span>, 'notes.visual.look.card', () => mentionToBlock('card'))}
              {mark('-', <span>{t('notes.visual.look.preview')}</span>, 'notes.visual.look.preview', () => mentionToBlock('preview'))}
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

        {/* A click on commented words opens their thread; the keyboard reaches it from the comments panel. */}
        <div
          onClick={(event) => {
            const thread = (event.target as HTMLElement).closest?.('[data-thread]')?.getAttribute('data-thread')
            if (thread && onOpenThread) onOpenThread(thread)
          }}
        >
          <EditorContent editor={editor} />
        </div>
      </div>

      {menu && <SuggestionMenu menu={menu} active={active} onHover={setActive} />}

      {editable && (
        <>
          {aiTarget && <AiTextPreview target={aiTarget} pageId={docId} onClose={() => setAiTarget(null)} onApply={(text, placement) => {
            if (!editor || !applyAiText(editor, aiTarget, text, placement, t('notes.ai.summaryTitle'))) {
              message.warning(t('notes.ai.documentChanged'))
              return
            }
            setAiTarget(null)
          }} />}
          <MediaPickerModal
            open={!!picking}
            kind={picking?.kind ?? null}
            onClose={() => {
              setPicking(null)
              pickResolver.current?.(null)
              pickResolver.current = null
              editor?.commands.focus()
            }}
            onPick={(item) => {
              setPicking(null)
              const resolve = pickResolver.current
              pickResolver.current = null
              if (resolve) resolve(item)
              else insertMedia(item)
            }}
          />
          <PagePickerModal
            open={!!pagePicker}
            pages={pagePicker === 'move' ? pages.filter((page) => page.id !== docId) : pages}
            onClose={() => {
              setPagePicker(null)
              editor?.commands.focus()
            }}
            onPick={(page) => {
              const purpose = pagePicker
              setPagePicker(null)
              if (purpose === 'move') void moveBlocks(page.id)
              else if (purpose) insertPage(page.id, purpose)
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
        </>
      )}
    </VisualEditorContext.Provider>
  )
}
