import { useEffect, useRef } from 'react'
import { minimalSetup } from 'codemirror'
import { EditorState } from '@codemirror/state'
import { EditorView, keymap, placeholder as cmPlaceholder } from '@codemirror/view'
import { defaultKeymap, history, historyKeymap } from '@codemirror/commands'
import { markdown } from '@codemirror/lang-markdown'
import { oneDark } from '@codemirror/theme-one-dark'
import type { AttachmentDto } from '../models'

interface MarkdownEditorProps {
  /** Changing this resets the document (a different page was opened). */
  docId: string
  initialValue: string
  placeholder?: string
  isDark: boolean
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
  onChange,
  onUpload,
}: MarkdownEditorProps) {
  const hostRef = useRef<HTMLDivElement>(null)
  const viewRef = useRef<EditorView | null>(null)

  // Keep the latest callbacks in refs so the editor isn't rebuilt when they change.
  const onChangeRef = useRef(onChange)
  const onUploadRef = useRef(onUpload)
  useEffect(() => {
    onChangeRef.current = onChange
    onUploadRef.current = onUpload
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

  return <div ref={hostRef} className="notes-editor" style={{ height: '100%', overflow: 'auto' }} />
}
