import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { NOTES_BASE } from '@/test/constants'
import { fetchMediaBlob, listMedia, uploadMedia } from './media.service'

describe('media.service', () => {
  it('uploads a file as multipart form data', async () => {
    let contentType: string | null = null
    server.use(
      // The service pins Content-Type to multipart/form-data; we assert on the header
      // rather than parsing the body (no boundary under jsdom).
      http.post(`${NOTES_BASE}/media`, ({ request }) => {
        contentType = request.headers.get('content-type')
        return HttpResponse.json({
          success: true,
          data: {
            id: 'a1',
            url: '/api/v1/notes/media/a1',
          },
        })
      }),
    )
    const file = new File(['data'], 'foto.png', { type: 'image/png' })
    const result = await uploadMedia(file, 'f1')

    expect(result.url).toBe('/api/v1/notes/media/a1')
    expect(contentType).toContain('multipart/form-data')
  })

  it('fetches an item blob through the authenticated endpoint', async () => {
    server.use(
      http.get(`${NOTES_BASE}/media/a1`, () =>
        HttpResponse.arrayBuffer(new TextEncoder().encode('png-bytes').buffer as ArrayBuffer, {
          headers: { 'Content-Type': 'image/png' },
        }),
      ),
    )
    const blob = await fetchMediaBlob('/api/v1/notes/media/a1')
    expect(blob).toBeInstanceOf(Blob)
  })

  it('asks for a folder, the unsorted items or all of them, leaving out what is not set', async () => {
    const queries: string[] = []
    server.use(
      http.get(`${NOTES_BASE}/media`, ({ request }) => {
        queries.push(new URL(request.url).search)
        return HttpResponse.json({ success: true, data: [] })
      }),
    )

    await listMedia({ folderId: 'f1', unsorted: false, kind: null, q: '' })
    await listMedia({ folderId: null, unsorted: true, kind: 'image', q: ' beach ' })

    expect(queries).toEqual(['?folderId=f1', '?unsorted=true&kind=image&q=beach'])
  })
})
