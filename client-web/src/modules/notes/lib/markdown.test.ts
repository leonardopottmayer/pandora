import { describe, expect, it } from 'vitest'
import type { Block } from './blocks'
import { blocksToHtml, blocksToMarkdown, markdownToBlocks, parseInline } from './markdown'

/** Blocks without their ids, which import makes fresh. */
const shape = (blocks: Block[]): unknown[] =>
  blocks.map((block) => {
    const rest = Object.fromEntries(Object.entries(block).filter(([key]) => key !== 'id' && key !== 'children'))
    return { ...rest, ...(block.children ? { children: shape(block.children) } : {}) }
  })

describe('live Agenda export', () => {
  it('exports a static label without serializing the source id or any live data', async () => {
    const blocks: Block[] = [{ id: 'agenda', type: 'agenda-list', props: { mode: 'tasks', listId: 'private-list-id' } },
      { id: 'events', type: 'agenda-list', props: { mode: 'events', calendarId: 'private-calendar', days: 30 } }]
    const md = blocksToMarkdown(blocks)
    const html = await blocksToHtml('Title', blocks, () => undefined, async () => null)
    expect(md).toContain('[Agenda]')
    expect(html).toContain('[Agenda]')
    expect(md).not.toContain('private-list-id')
    expect(html).not.toContain('private-list-id')
    expect(md).not.toContain('private-calendar')
    expect(html).not.toContain('private-calendar')
  })
})

describe('AI output export', () => {
  it('exports the accepted text to Markdown and HTML while keeping HTML-looking output inert', async () => {
    const blocks: Block[] = [{ id: 'a', type: 'ai', props: { prompt: 'Private prompt', output: [{ text: '<script>alert(1)</script>\nChecklist' }] } }]
    const md = blocksToMarkdown(blocks)
    expect(md).toContain('&lt;script&gt;')
    expect(md).toContain('Checklist')
    expect(md).not.toContain('Private prompt')
    const html = await blocksToHtml('Title', blocks, () => undefined, async () => null)
    expect(html).toContain('&lt;script&gt;alert(1)&lt;/script&gt;')
    expect(html).toContain('Checklist')
    expect(html).not.toContain('<script>')
  })
})

describe('markdownToBlocks', () => {
  it('reads headings, lists with nesting, to-dos, quotes, callouts, code, tables and rules', () => {
    const { title, blocks } = markdownToBlocks(
      [
        '# Outbox',
        '',
        'Grava no **mesmo** commit, ver [doc](https://a.example).',
        '',
        '## Passos',
        '- Pai',
        '  - Filho',
        '- [x] Feito',
        '1. Um',
        '',
        '> [!WARNING] Cuidado',
        '> Idempotência.',
        '',
        '> Citação',
        '',
        '```csharp',
        'var x = 1;',
        '```',
        '',
        '```mermaid',
        'graph TD; A-->B',
        '```',
        '',
        '| A | B |',
        '| --- | --- |',
        '| 1 | 2 |',
        '',
        '---',
      ].join('\n'),
    )
    expect(title).toBe('Outbox')
    expect(shape(blocks)).toEqual([
      { type: 'paragraph', content: [{ text: 'Grava no ' }, { text: 'mesmo', marks: [{ type: 'bold' }] }, { text: ' commit, ver ' }, { text: 'doc', marks: [{ type: 'link', attrs: { href: 'https://a.example' } }] }, { text: '.' }] },
      { type: 'heading', props: { level: 2 }, content: [{ text: 'Passos' }] },
      { type: 'bulleted', content: [{ text: 'Pai' }], children: [{ type: 'bulleted', content: [{ text: 'Filho' }] }] },
      { type: 'todo', props: { checked: true }, content: [{ text: 'Feito' }] },
      { type: 'numbered', content: [{ text: 'Um' }] },
      { type: 'callout', props: { tone: 'warning', title: 'Cuidado' }, children: [{ type: 'paragraph', content: [{ text: 'Idempotência.' }] }] },
      { type: 'quote', content: [{ text: 'Citação' }] },
      { type: 'code', props: { language: 'csharp' }, content: [{ text: 'var x = 1;' }] },
      { type: 'mermaid', props: { code: 'graph TD; A-->B' } },
      { type: 'table', props: { header: true, rows: [[[{ text: 'A' }], [{ text: 'B' }]], [[{ text: '1' }], [{ text: '2' }]]] } },
      { type: 'divider' },
    ])
  })

  it('makes [[Title]] a mention of a page it knows, and $x$ a formula', () => {
    expect(parseInline('Ver [[Roadmap]] e [[Nada]] com $x^2$', (title) => (title === 'Roadmap' ? 'p1' : undefined))).toEqual([
      { text: 'Ver ' }, { mention: { page: 'p1' } }, { text: ' e Nada com ' }, { math: { latex: 'x^2' } },
    ])
  })
})

describe('blocksToMarkdown', () => {
  it('exports only a Finance marker without source configuration or private figures', async () => {
    const blocks: Block[] = [{ id: 'f', type: 'finance-summary', props: { mode: 'account', accountId: 'private-bank' } }]
    expect(blocksToMarkdown(blocks, () => undefined).trim()).toBe('[Finances]')
    const html = await blocksToHtml('Finance', blocks, () => undefined, async () => '')
    expect(html).toContain('<p>[Finances]</p>')
    expect(html).not.toContain('private-bank')
  })

  it('writes what Markdown can say, and reads back the same', () => {
    const blocks: Block[] = [
      { id: 'h', type: 'heading', props: { level: 2 }, content: [{ text: 'Plano' }] },
      { id: 'p', type: 'paragraph', content: [{ text: 'Ver ' }, { mention: { page: 'p1' } }, { text: ' ' }, { text: 'agora', marks: [{ type: 'italic' }] }] },
      { id: 'l', type: 'bulleted', content: [{ text: 'Pai' }], children: [{ id: 'c', type: 'bulleted', content: [{ text: 'Filho' }] }] },
      { id: 't', type: 'todo', props: { checked: false }, content: [{ text: 'Fazer' }] },
      { id: 'q', type: 'callout', props: { tone: 'info', title: 'Nota' }, children: [{ id: 'qp', type: 'paragraph', content: [{ text: 'Dentro' }] }] },
    ]
    const markdown = blocksToMarkdown(blocks, (id) => (id === 'p1' ? 'Roadmap' : undefined))
    expect(markdown).toBe('## Plano\n\nVer [[Roadmap]] *agora*\n\n- Pai\n  - Filho\n\n- [ ] Fazer\n\n> [!INFO] Nota\n> Dentro\n')
    expect(shape(markdownToBlocks(markdown, (title) => (title === 'Roadmap' ? 'p1' : undefined)).blocks)).toEqual(shape(blocks))
  })
})

describe('blocksToHtml', () => {
  it('escapes the text, keeps only safe links and embeds images through the callback', async () => {
    const html = await blocksToHtml(
      'A <b>página</b>',
      [
        { id: 'p', type: 'paragraph', content: [{ text: '<script>x</script>', marks: [{ type: 'link', attrs: { href: 'javascript:alert(1)' } }] }] },
        { id: 'i', type: 'image', props: { mediaId: 'm1' }, content: [{ text: 'Praia' }] },
      ],
      () => undefined,
      async (id) => `data:image/png;base64,${id}`,
    )
    expect(html).toContain('<title>A &lt;b&gt;página&lt;/b&gt;</title>')
    expect(html).toContain('<a href="#">&lt;script&gt;x&lt;/script&gt;</a>')
    expect(html).toContain('<img src="data:image/png;base64,m1" alt="Praia">')
  })
})
