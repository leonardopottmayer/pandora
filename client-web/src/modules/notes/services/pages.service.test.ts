import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { NOTES_BASE } from '@/test/constants'
import {
  createPage,
  deletePage,
  getGraph,
  getLocalGraph,
  getPage,
  listPages,
  movePage,
  searchPages,
  setPageArchived,
  setPageFavorite,
  updatePage,
} from './pages.service'

const summary = { id: 'p1', parentId: null, title: 'Home', slug: 'home', icon: null, orderIndex: 0, isFavorite: false, isArchived: false }

describe('pages.service', () => {
  it('lists pages and forwards includeArchived', () => {
    let includeArchived: string | null = null
    server.use(
      http.get(`${NOTES_BASE}/pages`, ({ request }) => {
        includeArchived = new URL(request.url).searchParams.get('includeArchived')
        return HttpResponse.json({ success: true, data: [summary] })
      }),
    )
    return listPages(true).then((pages) => {
      expect(pages[0].title).toBe('Home')
      expect(includeArchived).toBe('true')
    })
  })

  it('gets a single page with its markdown body', async () => {
    server.use(
      http.get(`${NOTES_BASE}/pages/p1`, () =>
        HttpResponse.json({ success: true, data: { ...summary, contentMarkdown: '# Hi', createdAt: '', updatedAt: null } }),
      ),
    )
    const page = await getPage('p1')
    expect(page.contentMarkdown).toBe('# Hi')
  })

  it('creates a page', async () => {
    let body: unknown
    server.use(
      http.post(`${NOTES_BASE}/pages`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: { ...summary, id: 'p2', contentMarkdown: '' } })
      }),
    )
    const created = await createPage({ title: 'Nova', parentId: 'p1' })
    expect(created.id).toBe('p2')
    expect(body).toMatchObject({ title: 'Nova', parentId: 'p1' })
  })

  it('updates a page (the autosave path)', async () => {
    let body: unknown
    server.use(
      http.put(`${NOTES_BASE}/pages/p1`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: { ...summary, contentMarkdown: 'texto' } })
      }),
    )
    await updatePage('p1', { title: 'Home', icon: null, contentMarkdown: 'texto' })
    expect(body).toMatchObject({ title: 'Home', contentMarkdown: 'texto' })
  })

  it('moves a page with its new parent and order', async () => {
    let body: unknown
    server.use(
      http.post(`${NOTES_BASE}/pages/p1/move`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: { ...summary, contentMarkdown: '' } })
      }),
    )
    await movePage('p1', { parentId: 'p9', orderIndex: 2 })
    expect(body).toMatchObject({ parentId: 'p9', orderIndex: 2 })
  })

  it('hits the favorite/unfavorite routes', async () => {
    const paths: string[] = []
    server.use(
      http.post(`${NOTES_BASE}/pages/p1/:action`, ({ params }) => {
        paths.push(String(params.action))
        return HttpResponse.json({ success: true, data: { ...summary, contentMarkdown: '' } })
      }),
    )
    await setPageFavorite('p1', true)
    await setPageFavorite('p1', false)
    expect(paths).toEqual(['favorite', 'unfavorite'])
  })

  it('hits the archive/unarchive routes', async () => {
    const paths: string[] = []
    server.use(
      http.post(`${NOTES_BASE}/pages/p1/:action`, ({ params }) => {
        paths.push(String(params.action))
        return HttpResponse.json({ success: true, data: { ...summary, contentMarkdown: '' } })
      }),
    )
    await setPageArchived('p1', true)
    await setPageArchived('p1', false)
    expect(paths).toEqual(['archive', 'unarchive'])
  })

  it('searches pages with the term as the q parameter', async () => {
    let q: string | null = null
    server.use(
      http.get(`${NOTES_BASE}/pages/search`, ({ request }) => {
        q = new URL(request.url).searchParams.get('q')
        return HttpResponse.json({ success: true, data: [{ ...summary, excerpt: '...trecho...' }] })
      }),
    )
    const results = await searchPages('trecho')
    expect(q).toBe('trecho')
    expect(results[0].excerpt).toBe('...trecho...')
  })

  it('reads the global graph', async () => {
    server.use(
      http.get(`${NOTES_BASE}/pages/graph`, () =>
        HttpResponse.json({
          success: true,
          data: { nodes: [{ ...summary, degree: 1 }], edges: [{ sourceId: 'p1', targetId: 'p2', kind: 'wikilink' }] },
        }),
      ),
    )
    const graph = await getGraph()
    expect(graph.edges[0].kind).toBe('wikilink')
  })

  it('reads a local graph with the depth as a query parameter', async () => {
    let depth: string | null = null
    server.use(
      http.get(`${NOTES_BASE}/pages/p1/graph`, ({ request }) => {
        depth = new URL(request.url).searchParams.get('depth')
        return HttpResponse.json({ success: true, data: { nodes: [{ ...summary, degree: 0 }], edges: [] } })
      }),
    )
    const graph = await getLocalGraph('p1', 2)
    expect(depth).toBe('2')
    expect(graph.nodes).toHaveLength(1)
  })

  it('deletes a page', async () => {
    let called = false
    server.use(
      http.delete(`${NOTES_BASE}/pages/p1`, () => {
        called = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await deletePage('p1')
    expect(called).toBe(true)
  })
})
