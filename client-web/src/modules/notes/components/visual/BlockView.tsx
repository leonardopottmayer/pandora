import { useRef } from 'react'
import { NodeViewContent, NodeViewWrapper, type NodeViewProps } from '@tiptap/react'
import { Selection } from '@tiptap/pm/state'
import { Dropdown, Input, type InputRef } from 'antd'
import { useTranslation } from 'react-i18next'
import { CALLOUT_ICONS, CALLOUT_TYPES, type CalloutType } from '../../lib/callouts'

/** The title updates with each keystroke; the body belongs to the editor, not a nested editor. */
export function BlockView({ node, editor, getPos, updateAttributes }: NodeViewProps) {
  const { t } = useTranslation()
  const titleRef = useRef<InputRef>(null)
  const callout = node.type.name === 'callout'
  const type = node.attrs.calloutType as CalloutType
  const collapsible = !callout || node.attrs.fold !== null
  const open = callout ? node.attrs.fold !== '-' : !!node.attrs.open
  // A toggle with a level is a collapsible heading: its title reads as that heading.
  const level = callout ? null : (node.attrs.level as number | null)
  const titleLabel = callout ? t('notes.visual.calloutTitle') : level ? t('notes.visual.toggleHeadingTitle', { level }) : t('notes.visual.toggleTitle')

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
      className={`notes-native-block ${callout ? `notes-callout notes-callout-${type}` : level ? `notes-toggle-heading notes-toggle-h${level}` : 'notes-native-toggle'}`}
      data-collapsed={collapsible && !open}
    >
      <div className="notes-native-heading" contentEditable={false} {...(level ? { role: 'heading', 'aria-level': level } : {})}>
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
        {callout && editor.isEditable && (
          // The icon picks the tone: info, success, warning, error or a plain note.
          <Dropdown
            trigger={['click']}
            menu={{
              selectedKeys: [type],
              items: CALLOUT_TYPES.map((tone) => ({
                key: tone,
                icon: <span aria-hidden="true">{CALLOUT_ICONS[tone]}</span>,
                label: t(`notes.callout.${tone}`),
                onClick: () => updateAttributes({ calloutType: tone }),
              })),
            }}
          >
            <button type="button" className="notes-callout-icon notes-fold-button" aria-label={t('notes.visual.calloutTone')}>
              {CALLOUT_ICONS[type] ?? CALLOUT_ICONS.note}
            </button>
          </Dropdown>
        )}
        {callout && !editor.isEditable && <span className="notes-callout-icon" aria-hidden="true">{CALLOUT_ICONS[type] ?? CALLOUT_ICONS.note}</span>}
        <Input
          ref={titleRef}
          variant="borderless"
          className="notes-native-title"
          aria-label={titleLabel}
          placeholder={callout ? t(`notes.callout.${type}`) : level ? t(`notes.slash.h${level}`) : titleLabel}
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
