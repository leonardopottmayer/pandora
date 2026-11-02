import { describe, expect, it } from 'vitest'
import { Editor } from '@tiptap/core'
import { createMarkdownManager, serializeMarkdown, visualExtensions } from './visualMarkdown'

const manager = createMarkdownManager()
const roundTrip = (md: string) => serializeMarkdown(manager, manager.parse(md))

const IMAGE = '/api/v1/notes/media/aaaaaaaa-0000-0000-0000-000000000001'

describe('visual editor markdown', () => {
  // What the visual editor writes back must be what was there, for everything a note can hold.
  it.each([
    ['inline marks and links', 'Some **bold** and *it* and `code` and ~~gone~~ [link](https://x.com).'],
    ['headings', '# One\n\n## Two\n\n### Three'],
    ['lists, nested', '- a\n- b\n  - nested\n\n1. one\n2. two'],
    ['task lists', '- [ ] todo\n- [x] done'],
    ['a quote', '> quote line\n> two'],
    ['a fenced block with its language', '```ts\nconst a = 1\n```'],
    ['a divider', 'a\n\n---\n\nb'],
    ['wikilinks with alias and embed', 'See [[Page]], [[Page|that one]] and ![[Other]].'],
    ['tags', 'Filed under #trabalho and #ideias/novas'],
    ['a library image with its options', `![Beach|300|right](${IMAGE})`],
    ['an image with a title', `![a](${IMAGE} "Title")`],
    ['inline html', 'Press <kbd>Ctrl</kbd>+<kbd>K</kbd> or <span style="color: red">**not bold** here</span>.'],
    ['a library file, next to an image', `Ver [relatório_final*v2.pdf](${IMAGE}) e ![a](${IMAGE})`],
    ['a callout', '> [!tip] Title\n> body **b**\n> more'],
    ['a folding callout', '> [!warning]- Closed\n> hidden'],
    ['a details block across blank lines', '<details>\n<summary>S</summary>\n\nbody **b**\n\n</details>'],
    ['nested details', '<details>\n<summary>A</summary>\n\n<details>\n<summary>B</summary>\n\nx\n\n</details>\n\n</details>'],
    ['an iframe', '<iframe src="https://www.youtube.com/embed/x" height="300"></iframe>'],
    ['a video link alone on a line', 'https://youtu.be/dQw4w9WgXcQ'],
    ['a bare url with underscores', 'https://www.youtube.com/watch?v=a_b-c_d'],
    ['a hard break', 'line two  \nhard'],
    ['extra blank lines', 'para\n\n\n\nafter gap'],
  ])('keeps %s', (_, md) => {
    expect(roundTrip(md)).toBe(md)
  })

  it('keeps a table, alignment included', () => {
    const out = roundTrip('| a | b |\n|:--|--:|\n| 1 | 2 |')
    expect(out).toContain(':---')
    expect(out).toContain('---:')
    expect(roundTrip(out)).toBe(out)
  })

  it('keeps what follows a callout as separate editable blocks', () => {
    const json = manager.parse('> [!note] N\n> x\n\nafter')
    expect(json.content?.map((node) => node.type)).toEqual(['callout', 'paragraph'])
  })

  it('parses a rich callout body as editable nodes, including nested callouts', () => {
    const json = manager.parse('> [!tip]+ Title\n> **bold** [[Page]]\n>\n> - [ ] task\n>\n> > [!note] Inside\n> > nested')
    expect(json.content?.[0]).toMatchObject({
      type: 'callout', attrs: { calloutType: 'tip', title: 'Title', fold: '+' },
      content: [
        { type: 'paragraph', content: [{ type: 'text', marks: [{ type: 'bold' }] }, { type: 'text' }, { type: 'wikilink' }] },
        { type: 'taskList' },
        { type: 'callout', attrs: { title: 'Inside' } },
      ],
    })
    const md = serializeMarkdown(manager, json)
    expect(roundTrip(md)).toBe(md)
  })

  it.each([
    '> [!note]',
    '> [!tip]+ Title\n> a\n>\n> b',
    '<details open>\n<summary>A &amp; B &lt; C</summary>\n\ntext\n\n</details>',
    '<details>\n<summary>Code</summary>\n\n```html\n</details>\n```\n\nafter code\n\n</details>',
    '<details>\n<summary>Inline</summary>\n\nUse `</details>` here.\n\n</details>',
    '<details>\n<summary>Outer</summary>\n\n> [!info] Inner\n> body\n\n</details>',
  ])('preserves editable block syntax: %s', (md) => {
    expect(roundTrip(md)).toBe(md)
  })

  it('accepts a longer code fence without closing details at a tag inside the code', () => {
    const json = manager.parse('<details>\n<summary>Code</summary>\n\n```html\n</details>\n`````\n\nafter code\n\n</details>')
    expect(json.content?.[0]).toMatchObject({
      type: 'details',
      content: [
        { type: 'codeBlock', content: [{ type: 'text', text: '</details>' }] },
        { type: 'paragraph', content: [{ type: 'text', text: 'after code' }] },
      ],
    })
    const md = serializeMarkdown(manager, json)
    expect(roundTrip(md)).toBe(md)
  })

  it.each([
    '> [!unknown] Custom\n> body',
    '<details class="custom">\n<summary>S</summary>\n\nbody\n\n</details>',
    '<details>\n<summary><strong>Rich summary</strong></summary>\n\nbody\n\n</details>',
  ])('keeps unsupported blocks verbatim: %s', (md) => {
    expect(manager.parse(md).content?.[0].type).toBe('rawBlock')
    expect(roundTrip(md)).toBe(md)
  })

  it('keeps empty bodies valid and supports typing, undo and leaving a block', () => {
    const editor = new Editor({ extensions: visualExtensions(), content: manager.parse('> [!note]') })
    editor.commands.setTextSelection(2)
    editor.commands.insertContent('body')
    expect(serializeMarkdown(manager, editor.getJSON())).toBe('> [!note]\n> body')
    editor.commands.keyboardShortcut('Mod-Enter')
    expect(editor.getJSON().content?.map((node) => node.type)).toEqual(['callout', 'paragraph'])
    editor.commands.undo()
    expect(serializeMarkdown(manager, editor.getJSON())).toBe('> [!note]')
    editor.destroy()
  })

  it.each([
    '> [!warning]- Closed\n> **body**',
    '<details open>\n<summary>A &amp; B</summary>\n\nbody\n\n</details>',
  ])('keeps a block when copied and pasted as HTML: %s', (md) => {
    const editor = new Editor({ extensions: visualExtensions(), content: manager.parse(md) })
    editor.commands.setContent(editor.getHTML())
    expect(serializeMarkdown(manager, editor.getJSON())).toBe(md)
    editor.destroy()
  })

  it('keeps details pasted from another HTML renderer without our body wrapper', () => {
    const html = '<details open=""><summary>Title</summary><p>Body</p></details>'
    const editor = new Editor({ extensions: visualExtensions(), content: html })
    expect(editor.getJSON().content?.[0]).toMatchObject({ type: 'rawBlock', attrs: { source: html } })
    expect(serializeMarkdown(manager, editor.getJSON())).toBe(html)
    editor.destroy()
  })

  it('keeps two lists in a row apart, which markdown would merge into one', () => {
    const doc = {
      type: 'doc',
      content: [
        { type: 'taskList', content: [{ type: 'taskItem', attrs: { checked: false }, content: [{ type: 'paragraph', content: [{ type: 'text', text: 'a' }] }] }] },
        { type: 'paragraph' },
        { type: 'bulletList', content: [{ type: 'listItem', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'b' }] }] }] },
        { type: 'orderedList', content: [{ type: 'listItem', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'c' }] }] }] },
      ],
    }
    const md = serializeMarkdown(manager, doc)
    expect(md).toBe('- [ ] a\n\n\n\n<!-- -->\n\n- b\n\n1. c')
    // Loaded back, the break is gone and the lists are still two.
    expect(manager.parse(md).content?.map((node) => node.type)).toEqual(['taskList', 'paragraph', 'bulletList', 'orderedList'])
    expect(roundTrip(md)).toBe(md)
  })

  it('keeps an image alone on its line inside a paragraph, where an inline node has to be', () => {
    const json = manager.parse(`![a](${IMAGE})`)
    expect(json.content?.[0]).toMatchObject({ type: 'paragraph', content: [{ type: 'image' }] })
  })

  it('reads a wikilink as one node, not escaped text', () => {
    const json = manager.parse('[[Page|alias]]')
    expect(json.content?.[0].content?.[0]).toMatchObject({
      type: 'wikilink',
      attrs: { target: 'Page', alias: 'alias', embed: false },
    })
  })

  it('reads a library file link as one node, and any other link as a link', () => {
    const json = manager.parse(`[a.pdf](${IMAGE}) [site](https://x.com)`)
    expect(json.content?.[0].content?.[0]).toMatchObject({ type: 'mediaFile', attrs: { href: IMAGE, label: 'a.pdf' } })
    expect(json.content?.[0].content?.[2]).toMatchObject({ type: 'text', marks: [{ type: 'link' }] })
  })

  it('copies a library file and pastes it back as the same file', () => {
    const editor = new Editor({ extensions: visualExtensions(), content: manager.parse(`[a.pdf](${IMAGE})`) })
    editor.commands.setContent(editor.getHTML())
    expect(serializeMarkdown(manager, editor.getJSON())).toBe(`[a.pdf](${IMAGE})`)
    editor.destroy()
  })

  it('keeps inline html through a copy and paste', () => {
    const md = 'Press <kbd>Ctrl</kbd> now'
    const editor = new Editor({ extensions: visualExtensions(), content: manager.parse(md) })
    editor.commands.setContent(editor.getHTML())
    expect(serializeMarkdown(manager, editor.getJSON())).toBe(md)
    editor.destroy()
  })

  it('turns an inline html image into markdown, width and all', () => {
    expect(roundTrip(`text <img src="${IMAGE}" width="200"> more`)).toBe(`text ![|200](${IMAGE}) more`)
  })

  it('copies a library image without a src the browser would fetch with no token, and pastes it back', () => {
    const editor = new Editor({ extensions: visualExtensions(), content: `<p><img src="${IMAGE}" alt="a"></p>` })
    const html = editor.getHTML()
    expect(html).toContain(`data-media-src="${IMAGE}"`)
    expect(html).not.toContain(' src=')

    editor.commands.setContent(html)
    expect(editor.getJSON().content?.[0].content?.[0]).toMatchObject({ type: 'image', attrs: { src: IMAGE, alt: 'a' } })
    editor.destroy()
  })

  it('normalizes without changing what renders', () => {
    expect(roundTrip('* star\n* two')).toBe('- star\n- two')
    expect(roundTrip('_it_ __b__')).toBe('*it* **b**')
  })
})
