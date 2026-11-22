import { beforeAll, describe, expect, it, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { NOTES_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { MediaItemDto } from '../../models'
import { MediaPickerModal } from './MediaPickerModal'

const ok = (data: unknown) => HttpResponse.json({ success: true, data })

const beach: MediaItemDto = {
  id: 'm1',
  folderId: null,
  kind: 'image',
  name: 'beach.png',
  originalFileName: 'beach.png',
  contentType: 'image/png',
  sizeBytes: 100,
  width: 10,
  height: 10,
  url: '/api/v1/notes/media/m1',
  thumbnailUrl: null,
  usageCount: 0,
  isDeleted: false,
  createdAt: '2026-11-02T10:00:00Z',
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
  URL.createObjectURL = vi.fn(() => 'blob:fake')
})

describe('MediaPickerModal', () => {
  it('asks for images only when picking an image, and hands back the one clicked', async () => {
    const listed: string[] = []
    server.use(
      http.get(`${NOTES_BASE}/media-folders`, () => ok({ folders: [], unsortedCount: 1, unusedCount: 1, totalCount: 1 })),
      http.get(`${NOTES_BASE}/media`, ({ request }) => {
        listed.push(new URL(request.url).search)
        return ok([beach])
      }),
      http.get(`${NOTES_BASE}/media/:id`, () => new HttpResponse(new Blob(['x']))),
    )
    const onPick = vi.fn()
    const user = userEvent.setup()
    renderWithProviders(<MediaPickerModal open kind="image" onPick={onPick} onClose={() => {}} />)

    await user.click(await screen.findByRole('button', { name: 'beach.png' }))

    expect(listed[0]).toBe('?kind=image')
    expect(onPick).toHaveBeenCalledWith(beach)
  })
})
