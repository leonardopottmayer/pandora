import { Extension } from '@tiptap/core'
import { Plugin } from '@tiptap/pm/state'
import { Decoration, DecorationSet } from '@tiptap/pm/view'
import type { Node as PmNode } from '@tiptap/pm/model'
import { lockFrame, videoEmbedUrl } from '../../lib/embeds'

// A video link alone on its line draws as the player in reading mode; here it draws the player too,
// under the link, which stays to be edited. The player is a decoration: the document, and so the
// markdown, is still just the link.

function player(src: string, title: string) {
  const box = document.createElement('div')
  box.className = 'notes-video'
  box.contentEditable = 'false'
  const frame = document.createElement('iframe')
  frame.src = src
  frame.title = title
  lockFrame(frame)
  box.append(frame)
  return box
}

function players(doc: PmNode) {
  const widgets: Decoration[] = []
  doc.descendants((node, pos) => {
    if (node.type.name !== 'paragraph') return true
    const link = node.textContent.trim()
    const src = /\s/.test(link) ? null : videoEmbedUrl(link)
    // The key keeps the same frame across keystrokes elsewhere, so the video does not reload.
    if (src) widgets.push(Decoration.widget(pos + node.nodeSize, () => player(src, link), { key: src, side: -1 }))
    return false
  })
  return DecorationSet.create(doc, widgets)
}

export const VideoPreview = Extension.create({
  name: 'videoPreview',
  addProseMirrorPlugins() {
    const plugin: Plugin<DecorationSet> = new Plugin({
      state: {
        init: (_, { doc }) => players(doc),
        apply: (tr, set) => (tr.docChanged ? players(tr.doc) : set),
      },
      props: { decorations: (state) => plugin.getState(state) },
    })
    return [plugin]
  },
})
