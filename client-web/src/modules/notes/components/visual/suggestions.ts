import { Extension, type Editor, type Range } from '@tiptap/core'
import { PluginKey } from '@tiptap/pm/state'
import Suggestion from '@tiptap/suggestion'

// The `/`, `[[` and `#` menus of the visual editor. Each trigger is a TipTap suggestion; all of them
// draw through one React menu, which the editor component owns — the bridge is how they reach it.

export interface MenuEntry {
  key: string
  /** An emoji before the label: a page's own icon. */
  icon?: string
  label: string
  /** Muted text beside the label: the slug of a page. */
  hint?: string
  /** The category it is listed under; a heading opens each new one. */
  section?: string
}

export interface MenuState {
  entries: MenuEntry[]
  rect: DOMRect | null
  pick: (index: number) => void
}

export interface MenuBridge {
  show: (state: MenuState) => void
  hide: () => void
  /** Arrow keys, Enter and Esc while a menu is open; true when the menu took the key. */
  keyDown: (event: KeyboardEvent) => boolean
}

interface TriggerOptions<T> {
  name: string
  char: string
  allowSpaces?: boolean
  /** What the typed query offers; may be read from the server. */
  items: (query: string) => T[] | Promise<T[]>
  entry: (item: T) => MenuEntry
  /** Replaces the typed trigger (`range`) with what the item stands for. */
  apply: (editor: Editor, range: Range, item: T) => void
}

export function menuTrigger<T>(bridge: MenuBridge, options: TriggerOptions<T>) {
  return Extension.create({
    name: options.name,
    addProseMirrorPlugins() {
      return [
        Suggestion<T, T>({
          editor: this.editor,
          pluginKey: new PluginKey(options.name),
          char: options.char,
          allowSpaces: options.allowSpaces ?? false,
          items: ({ query }) => options.items(query),
          command: ({ editor, range, props }) => options.apply(editor, range, props),
          render: () => {
            const show = (props: { items: T[]; clientRect?: (() => DOMRect | null) | null; command: (item: T) => void }) =>
              bridge.show({
                entries: props.items.map(options.entry),
                rect: props.clientRect?.() ?? null,
                pick: (index) => props.command(props.items[index]),
              })
            return {
              onStart: show,
              onUpdate: show,
              onExit: () => bridge.hide(),
              onKeyDown: ({ event }) => bridge.keyDown(event),
            }
          },
        }),
      ]
    },
  })
}
