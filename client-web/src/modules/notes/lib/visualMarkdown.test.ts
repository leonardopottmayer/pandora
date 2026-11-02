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

  it('keeps what follows a raw block as editable blocks', () => {
    const json = manager.parse('> [!note] N\n> x\n\nafter')
    expect(json.content?.map((node) => node.type)).toEqual(['rawBlock', 'paragraph'])
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
