import { describe, expect, it } from 'vitest'
import { documentLines, tagsOf, toBlocks, toEditorDoc, withTags, type Block } from './blocks'

const blocks: Block[] = [
  { id: 'h1', type: 'heading', props: { level: 2 }, content: [{ text: 'Outbox' }] },
  {
    id: 'p1',
    type: 'paragraph',
    content: [{ text: 'Ver ' }, { mention: { page: 'page-1' } }, { text: ' agora', marks: [{ type: 'bold' }] }, { text: '\nfim' }],
  },
  { id: 'l1', type: 'bulleted', content: [{ text: 'Pai' }], children: [{ id: 'l2', type: 'bulleted', content: [{ text: 'Filho' }] }] },
  { id: 'l3', type: 'bulleted', content: [{ text: 'Irmão' }] },
  { id: 't1', type: 'todo', props: { checked: true }, content: [{ text: 'Feito' }] },
  { id: 'q1', type: 'quote', content: [{ text: 'Citação' }] },
  { id: 'c1', type: 'code', props: { language: 'csharp' }, content: [{ text: 'var x = 1;\nvar y = 2;' }] },
  { id: 'd1', type: 'divider' },
  { id: 'co', type: 'callout', props: { tone: 'info', title: 'Dica' }, children: [{ id: 'cp', type: 'paragraph', content: [{ text: 'Dentro' }] }] },
  { id: 'tg', type: 'toggle', props: { open: true }, content: [{ text: 'Abrir' }], children: [{ id: 'tp', type: 'paragraph', content: [{ text: 'Escondido' }] }] },
  { id: 'tb', type: 'table', props: { header: true, rows: [[[{ text: 'A' }], [{ text: 'B' }]], [[{ text: '1' }], []]] } },
  { id: 'im', type: 'image', props: { mediaId: 'm-1', width: 300, align: 'center' }, content: [{ text: 'Legenda' }] },
  { id: 'fi', type: 'file', props: { mediaId: 'm-2', name: 'a.pdf' } },
  { id: 'fu', type: 'kanban-board', props: { columns: 3 } },
]

describe('blocks', () => {
  it('keeps Finance configuration and identity through editor and version conversions', () => {
    const finance: Block[] = [{ id: 'f1', type: 'finance-summary', props: { mode: 'expenses', month: '2026-11' } },
      { id: 'f2', type: 'finance-summary', props: { mode: 'account', accountId: 'bank' } }]
    expect(toBlocks(toEditorDoc(finance).content)).toEqual(finance)
    expect(documentLines(finance, () => undefined)).toEqual(['[finance-summary]', '[finance-summary]'])
  })

  it('keeps Agenda configuration and block identity through the editor without a data snapshot', () => {
    const agenda: Block[] = [{ id: 'agenda1', type: 'agenda-list', props: { mode: 'tasks', listId: 'list1' } },
      { id: 'agenda2', type: 'agenda-list', props: { mode: 'events', days: 30, calendarId: 'calendar1' } }]
    expect(toEditorDoc(agenda).content?.[0].type).toBe('richBlock')
    expect(toBlocks(toEditorDoc(agenda).content)).toEqual(agenda)
    expect(documentLines(agenda, () => undefined)).toEqual(['[agenda-list]', '[agenda-list]'])
  })

  it('keeps AI block properties and ids through conversion and shows prompt/output in version diffs', () => {
    const ai: Block[] = [{ id: 'ai1', type: 'ai', props: {
      prompt: 'Write a checklist', output: [{ text: 'Review tests\nCheck scope' }], generatedAt: '2026-11-07T18:00:00Z',
    } }]
    expect(toEditorDoc(ai).content?.[0].type).toBe('richBlock')
    expect(toBlocks(toEditorDoc(ai).content)).toEqual(ai)
    expect(documentLines(ai, () => undefined)).toEqual(['[ai] Write a checklist', 'Review tests', 'Check scope'])
  })

  it('go to the editor and back unchanged, ids included', () => {
    expect(toBlocks(toEditorDoc(blocks).content)).toEqual(blocks)
  })

  it('keep colours, alignment, collapsible headings, columns and page links through the editor', () => {
    const b3: Block[] = [
      { id: 'sp', type: 'paragraph', props: { color: 'red', background: 'blue', align: 'center' }, content: [{ text: 'Pintado' }] },
      { id: 'sl', type: 'bulleted', props: { color: 'green' }, content: [{ text: 'Item' }] },
      { id: 'sm', type: 'paragraph', content: [{ text: 'cor', marks: [{ type: 'color', attrs: { color: 'red' } }] }, { text: 'fundo', marks: [{ type: 'highlight', attrs: { color: 'yellow' } }] }] },
      { id: 'cm', type: 'paragraph', content: [{ text: 'Prazo em ' }, { text: 'março', marks: [{ type: 'comment', attrs: { thread: 't1' } }, { type: 'comment', attrs: { thread: 't2' } }] }] },
      { id: 'sh', type: 'heading', props: { level: 2, toggle: true, open: true, background: 'gray' }, content: [{ text: 'Seção' }], children: [{ id: 'sc', type: 'paragraph', content: [{ text: 'Corpo' }] }] },
      { id: 'cs', type: 'columns', children: [
        { id: 'k1', type: 'column', children: [{ id: 'k1p', type: 'paragraph', content: [{ text: 'Esquerda' }] }] },
        { id: 'k2', type: 'column', children: [{ id: 'k2p', type: 'paragraph', content: [{ text: 'Direita' }] }] },
      ] },
      { id: 'pl', type: 'page-link', props: { pageId: 'page-1' } },
      { id: 'pp', type: 'page-link', props: { pageId: 'page-2', look: 'preview' } },
      { id: 'bm', type: 'bookmark', props: { url: 'https://a.example', title: 'A', imageUrl: 'https://a.example/i.png' } },
      { id: 'em', type: 'embed', props: { url: 'https://youtu.be/dQw4w9WgXcQ' } },
      { id: 'vd', type: 'video', props: { url: 'https://a.example/v.mp4' } },
      { id: 'au', type: 'audio', props: { mediaId: 'm-3', name: 'aula.mp3' } },
      { id: 'pd', type: 'pdf', props: { mediaId: 'm-4' } },
      { id: 'ga', type: 'gallery', props: { items: [{ mediaId: 'm-5' }, { mediaId: 'm-6', caption: 'Praia' }] } },
      { id: 'mm', type: 'mermaid', props: { code: 'graph TD; A-->B' } },
      { id: 'mt', type: 'math', props: { latex: 'E = mc^2' } },
      { id: 'sy', type: 'synced', children: [{ id: 'syp', type: 'paragraph', content: [{ text: 'Compartilhado' }] }] },
      { id: 'sr', type: 'synced-ref', props: { pageId: 'page-1', blockId: 'sy' } },
      { id: 'tb', type: 'template-button', props: { label: 'Nova reunião' }, children: [{ id: 'tbp', type: 'todo', content: [{ text: 'Pauta' }] }] },
      { id: 'dc', type: 'decision', props: { status: 'decided' }, content: [{ text: 'Usar outbox' }] },
      { id: 'do', type: 'decision', content: [{ text: 'Em aberto' }] },
      { id: 'bc', type: 'breadcrumb' },
      { id: 'sp2', type: 'subpages' },
      { id: 'tp', type: 'tag-pages', props: { tags: ['dotnet'] } },
      { id: 'pr', type: 'properties', props: { rows: [{ key: [{ text: 'Dono' }], value: [{ text: 'Ana' }] }] } },
      { id: 'ch', type: 'paragraph', content: [
        { text: 'Entrega ' }, { date: { value: '2026-12-01T14:00', reminderId: 'r1' } }, { text: ' ' },
        { status: { label: 'Bloqueado', color: 'red' } }, { text: ' ' },
        { item: { module: 'agenda', kind: 'task', id: 't1', label: 'Revisar' } }, { text: ' ' }, { math: { latex: 'x^2' } },
      ] },
    ]
    expect(toBlocks(toEditorDoc(b3).content)).toEqual(b3)
  })

  it('give a lone column a neighbour, the least the editor holds', () => {
    const doc = toEditorDoc([{ id: 'cs', type: 'columns', children: [{ id: 'k1', type: 'column' }] }])
    expect(doc.content?.[0].content).toHaveLength(2)
  })

  it('give consecutive list items of one kind one list', () => {
    const doc = toEditorDoc(blocks)
    expect(doc.content?.filter((node) => node.type === 'bulletList')).toHaveLength(1)
  })

  it('leave the tags block to the header and keep it first with its id', () => {
    const withTagsBlock = withTags(blocks, ['dotnet'])
    expect(toEditorDoc(withTagsBlock).content?.some((node) => node.type === 'tags')).toBe(false)
    expect(tagsOf(withTagsBlock)).toEqual(['dotnet'])
    const again = withTags(withTagsBlock, ['dotnet', 'tars'])
    expect(again[0]).toEqual({ id: withTagsBlock[0].id, type: 'tags', props: { tags: ['dotnet', 'tars'] } })
    expect(withTags(again, [])).toEqual(blocks)
  })

  it('give an empty document one empty paragraph to type in', () => {
    expect(toEditorDoc([])).toEqual({ type: 'doc', content: [{ type: 'paragraph' }] })
  })

  it('read as lines for a version diff, mentions by title', () => {
    expect(documentLines(blocks.slice(0, 5), (id) => (id === 'page-1' ? 'Eventos' : undefined))).toEqual([
      '## Outbox',
      'Ver @Eventos agora',
      'fim',
      '- Pai',
      '  - Filho',
      '- Irmão',
      '- [x] Feito',
    ])
  })
})
