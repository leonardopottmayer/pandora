import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { NOTES_BASE } from '@/test/constants'
import { fetchAttachmentBlob, uploadAttachment } from './attachments.service'

describe('attachments.service', () => {
  it('uploads a file as multipart form data', async () => {
    let contentType: string | null = null
    server.use(
      // The service pins Content-Type to multipart/form-data; we assert on the header
      // rather than parsing the body (no boundary under jsdom).
      http.post(`${NOTES_BASE}/attachments`, ({ request }) => {
        contentType = request.headers.get('content-type')
        return HttpResponse.json({
          success: true,
          data: {
            id: 'a1',
            pageId: 'p1',
            fileName: 'foto.png',
            contentType: 'image/png',
            sizeBytes: 4,
            url: '/api/v1/notes/attachments/a1',
            createdAt: '',
          },
        })
      }),
    )
    const file = new File(['data'], 'foto.png', { type: 'image/png' })
    const result = await uploadAttachment(file, 'p1')

    expect(result.url).toBe('/api/v1/notes/attachments/a1')
    expect(contentType).toContain('multipart/form-data')
  })

  it('fetches an attachment blob through the authenticated endpoint', async () => {
    server.use(
      http.get(`${NOTES_BASE}/attachments/a1`, () =>
        HttpResponse.arrayBuffer(new TextEncoder().encode('png-bytes').buffer as ArrayBuffer, {
          headers: { 'Content-Type': 'image/png' },
        }),
      ),
    )
    const blob = await fetchAttachmentBlob('/api/v1/notes/attachments/a1')
    expect(blob).toBeInstanceOf(Blob)
  })
})
