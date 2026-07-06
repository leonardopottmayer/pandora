import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import {
  abortImportFile,
  getImportFile,
  getImportRows,
  listImportFiles,
  listImportLayouts,
  retryImportFile,
  uploadImportFile,
} from './imports.service'
import type { ImportFileDto } from '../models'

const importFile: ImportFileDto = {
  id: 'imp1',
  userId: 'u1',
  layoutId: 'lay1',
  accountId: 'a1',
  cardId: null,
  fileName: 'extrato.ofx',
  fileHash: 'hash',
  fileSize: 1024,
  correlationId: 'corr1',
  cutoffDate: null,
  status: 'completed',
  totalRows: 10,
  parsedRows: 9,
  errorRows: 1,
  duplicateRows: 0,
  suggestionRows: 2,
  retryCount: 0,
  errorMessage: null,
  startedAt: '2026-06-13T00:00:00Z',
  completedAt: '2026-06-13T00:01:00Z',
  createdAt: '2026-06-13T00:00:00Z',
}

describe('imports.service', () => {
  it('lists import files forwarding skip/take', async () => {
    let seenUrl: URL | undefined
    server.use(
      http.get(`${FINANCES_BASE}/imports`, ({ request }) => {
        seenUrl = new URL(request.url)
        return HttpResponse.json({ success: true, data: [importFile] })
      }),
    )
    const result = await listImportFiles({ skip: 5, take: 10 })
    expect(result).toHaveLength(1)
    expect(seenUrl?.searchParams.get('skip')).toBe('5')
    expect(seenUrl?.searchParams.get('take')).toBe('10')
  })

  it('fetches a single import file', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/imports/imp1`, () =>
        HttpResponse.json({ success: true, data: importFile }),
      ),
    )
    expect((await getImportFile('imp1')).id).toBe('imp1')
  })

  it('fetches the rows of an import file', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/imports/imp1/rows`, () =>
        HttpResponse.json({ success: true, data: [] }),
      ),
    )
    expect(await getImportRows('imp1')).toEqual([])
  })

  it('uploads a file as multipart form data', async () => {
    let contentType: string | null = null
    server.use(
      // Note: the service pins Content-Type to multipart/form-data; we assert
      // on the header rather than parsing the body (no boundary under jsdom).
      http.post(`${FINANCES_BASE}/imports`, ({ request }) => {
        contentType = request.headers.get('content-type')
        return HttpResponse.json({ success: true, data: importFile })
      }),
    )
    const file = new File(['data'], 'extrato.ofx', { type: 'text/plain' })
    const result = await uploadImportFile(file, 'a1')
    expect(result.id).toBe('imp1')
    expect(contentType).toContain('multipart/form-data')
  })

  it('aborts an import file', async () => {
    let called = false
    server.use(
      http.post(`${FINANCES_BASE}/imports/imp1/abort`, () => {
        called = true
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await abortImportFile('imp1')
    expect(called).toBe(true)
  })

  it('retries an import file', async () => {
    let called = false
    server.use(
      http.post(`${FINANCES_BASE}/imports/imp1/retry`, () => {
        called = true
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await retryImportFile('imp1')
    expect(called).toBe(true)
  })

  it('lists import layouts', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/import-layouts`, () =>
        HttpResponse.json({ success: true, data: [] }),
      ),
    )
    expect(await listImportLayouts()).toEqual([])
  })
})
