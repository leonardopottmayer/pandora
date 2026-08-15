import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { minimalSetup } from 'codemirror'
import { EditorState } from '@codemirror/state'
import { EditorView, keymap, placeholder as cmPlaceholder, tooltips } from '@codemirror/view'
import { defaultKeymap, history, historyKeymap } from '@codemirror/commands'
import { markdown } from '@codemirror/lang-markdown'
import { oneDark } from '@codemirror/theme-one-dark'
import { autocompletion } from '@codemirror/autocomplete'
import {
  insertTable,
  moveTableCell,
  slashSource,
  tagSource,
  wikilinkSource,
} from '../lib/editorCommands'
import { TableSizeModal } from './TableSizeModal'
import type { AttachmentDto, PageSummaryDto, TagDto } from '../models'

interface MarkdownEditorProps {
  /** Changing this resets the document (a different page was opened). */
  docId: string
  initialValue: string
  placeholder?: string
  isDark: boolean
  /** Targets offered by the `[[` autocomplete. */
  pages: PageSummaryDto[]
  /** Tags offered by the `#` autocomplete — the ones that already exist somewhere. */
  tags: TagDto[]
  onChange: (value: string) => void
  /** Uploads a pasted/dropped file and returns its attachment metadata for embedding. */
  onUpload: (file: File) => Promise<AttachmentDto>
}

/** Only images embed inline as `![]`; other files embed as a plain markdown link. */
function embedMarkdown(a: AttachmentDto): string {
  const isImage = a.contentType.startsWith('image/')
  return isImage ? `![${a.fileName}](${a.url})` : `[${a.fileName}](${a.url})`
}

export function MarkdownEditor({
  docId,
  initialValue,
  placeholder,
  isDark,
  pages,
  tags,
  onChange,
  onUpload,
}: MarkdownEditorProps) {
  const { t } = useTranslation()
  const hostRef = useRef<HTMLDivElement>(null)
  const viewRef = useRef<EditorView | null>(null)
  // `/table` asked for a size and is waiting on the dialog.
  const [tableSizeOpen, setTableSizeOpen] = useState(false)

  // Keep the latest callbacks in refs so the editor isn't rebuilt when they change.
  const onChangeRef = useRef(onChange)
  const onUploadRef = useRef(onUpload)
  const tRef = useRef(t)
  const pagesRef = useRef(pages)
  const tagsRef = useRef(tags)
  useEffect(() => {
    onChangeRef.current = onChange
    onUploadRef.current = onUpload
    tRef.current = t
    pagesRef.current = pages
    tagsRef.current = tags
  })

  // Inserts markdown at the current selection (used after an async upload completes).
  function insertAtCursor(view: EditorView, text: string) {
    const { from, to } = view.state.selection.main
    view.dispatch({
      changes: { from, to, insert: text },
      selection: { anchor: from + text.length },
    })
    view.focus()
  }

  async function handleFiles(view: EditorView, files: FileList) {
    for (const file of Array.from(files)) {
      const attachment = await onUploadRef.current(file)
      insertAtCursor(view, embedMarkdown(attachment))
    }
  }

  // (Re)build the editor whenever the open page or the theme changes.
  useEffect(() => {
    if (!hostRef.current) return

    // Labels and page list are read through refs, so neither a language switch nor a new page
    // rebuilds the editor.
    const slashCompletions = slashSource(
      (command) => tRef.current(command.labelKey),
      () => setTableSizeOpen(true),
    )
    const wikilinkCompletions = wikilinkSource(() => pagesRef.current)
    const tagCompletions = tagSource(() => tagsRef.current)

    const uploadHandlers = EditorView.domEventHandlers({
      paste(event, view) {
        const files = event.clipboardData?.files
        if (files && files.length > 0) {
          event.preventDefault()
          void handleFiles(view, files)
          return true
        }
        return false
      },
      drop(event, view) {
        const files = event.dataTransfer?.files
        if (files && files.length > 0) {
          event.preventDefault()
          void handleFiles(view, files)
          return true
        }
        return false
      },
    })

    const extensions = [
      minimalSetup,
      history(),
      autocompletion({
        override: [slashCompletions, wikilinkCompletions, tagCompletions],
        icons: false,
      }),
      // The editor sits inside a Card with `overflow: hidden`, which would clip the menu near the
      // bottom of the pane; hanging the tooltip off the body keeps it whole.
      tooltips({ parent: document.body }),
      // Ahead of the default keymap: inside a table Tab walks cells, everywhere else it declines.
      keymap.of([
        { key: 'Tab', run: moveTableCell(1) },
        { key: 'Shift-Tab', run: moveTableCell(-1) },
      ]),
      keymap.of([...defaultKeymap, ...historyKeymap]),
      markdown(),
      EditorView.lineWrapping,
      uploadHandlers,
      EditorView.updateListener.of((update) => {
        if (update.docChanged) onChangeRef.current(update.state.doc.toString())
      }),
      ...(placeholder ? [cmPlaceholder(placeholder)] : []),
      ...(isDark ? [oneDark] : []),
    ]

    const view = new EditorView({
      parent: hostRef.current,
      state: EditorState.create({ doc: initialValue, extensions }),
    })
    viewRef.current = view

    return () => {
      view.destroy()
      viewRef.current = null
    }
    // initialValue/placeholder intentionally excluded: they seed the doc only on (re)build,
    // keyed by docId so switching pages resets content without clobbering live typing.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [docId, isDark])

  // Either way the editor takes the focus back: the dialog took it, and the cursor is still sitting
  // where the `/table` was typed.
  function closeTableSize() {
    setTableSizeOpen(false)
    viewRef.current?.focus()
  }

  return (
    <>
      <div ref={hostRef} className="notes-editor" style={{ height: '100%', overflow: 'auto' }} />
      {tableSizeOpen && (
        <TableSizeModal
          onCancel={closeTableSize}
          onConfirm={(rows, columns) => {
            if (viewRef.current) insertTable(viewRef.current, rows, columns)
            closeTableSize()
          }}
        />
      )}
    </>
  )
}
