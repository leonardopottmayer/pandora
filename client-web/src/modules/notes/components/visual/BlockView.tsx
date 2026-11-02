import { useRef } from 'react'
import { NodeViewContent, NodeViewWrapper, type NodeViewProps } from '@tiptap/react'
import { Selection } from '@tiptap/pm/state'
import { Input, type InputRef } from 'antd'
import { useTranslation } from 'react-i18next'
import { CALLOUT_ICONS, type CalloutType } from '../../lib/callouts'

/** The title updates with each keystroke; the body belongs to the editor, not a nested editor. */
export function BlockView({ node, editor, getPos, updateAttributes }: NodeViewProps) {
  const { t } = useTranslation()
  const titleRef = useRef<InputRef>(null)
  const callout = node.type.name === 'callout'
  const type = node.attrs.calloutType as CalloutType
  const collapsible = !callout || node.attrs.fold !== null
  const open = callout ? node.attrs.fold !== '-' : !!node.attrs.open
  const titleLabel = callout ? t('notes.visual.calloutTitle') : t('notes.visual.toggleTitle')

  function focusBody() {
    const pos = getPos()
    if (typeof pos !== 'number') return
    if (collapsible && !open) updateAttributes(callout ? { fold: '+' } : { open: true })
    editor.chain().command(({ tr }) => {
      tr.setSelection(Selection.near(tr.doc.resolve(pos + 1)))
      return true
    }).focus(undefined, { scrollIntoView: false }).run()
  }

  return (
    <NodeViewWrapper
      className={`notes-native-block ${callout ? `notes-callout notes-callout-${type}` : 'notes-native-toggle'}`}
      data-collapsed={collapsible && !open}
    >
      <div className="notes-native-heading" contentEditable={false}>
        {collapsible && (
          <button
            type="button"
            className="notes-fold-button"
            aria-label={t(open ? 'notes.visual.collapseBlock' : 'notes.visual.expandBlock')}
            aria-expanded={open}
            onMouseDown={(event) => event.preventDefault()}
            onClick={() => {
              updateAttributes(callout ? { fold: open ? '-' : '+' } : { open: !open })
              // A caret in the body must not remain hidden after folding it.
              if (open) titleRef.current?.focus()
            }}
          >
            <span aria-hidden="true">{open ? '▾' : '▸'}</span>
          </button>
        )}
        {callout && <span className="notes-callout-icon" aria-hidden="true">{CALLOUT_ICONS[type]}</span>}
        <Input
          ref={titleRef}
          variant="borderless"
          className="notes-native-title"
          aria-label={titleLabel}
          placeholder={callout ? t(`notes.callout.${type}`) : titleLabel}
          value={String(node.attrs.title)}
          autoFocus={!!node.attrs.autoFocus}
          onChange={(event) => updateAttributes({ title: event.target.value })}
          onKeyDown={(event) => {
            if ((event.ctrlKey || event.metaKey) && ['z', 'y'].includes(event.key.toLowerCase())) {
              event.preventDefault()
              if (event.shiftKey || event.key.toLowerCase() === 'y') editor.commands.redo()
              else editor.commands.undo()
            } else if (event.key === 'Enter' || event.key === 'ArrowDown') {
              event.preventDefault()
              if (event.ctrlKey || event.metaKey) {
                const pos = getPos()
                if (typeof pos === 'number') {
                  const after = pos + node.nodeSize
                  const next = editor.state.doc.nodeAt(after)
                  if (next?.type.name === 'paragraph' && next.content.size === 0) editor.commands.focus(after + 1)
                  else editor.chain().insertContentAt(after, { type: 'paragraph' }).focus(after + 1).run()
                }
              } else focusBody()
            }
          }}
        />
      </div>
      <NodeViewContent className="notes-callout-body notes-native-body" />
    </NodeViewWrapper>
  )
}
