import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { TreeProps } from 'antd'
import { http, HttpResponse } from 'msw'
import i18n from '@/i18n'
import { NOTES_BASE } from '@/test/constants'
import { server } from '@/test/msw/server'
import { renderWithProviders } from '@/test/utils'
import * as access from '@/modules/identity/hooks/useAccess'
import { NotesSidebar } from './NotesSidebar'

// Exercise the sidebar-to-API wiring through Tree's public drop contract. Native drag geometry
// remains a browser check; the destination/order calculation has its own spaceTree tests.
vi.mock('antd', async (original) => {
  const actual = await original<typeof import('antd')>()
  return {
    ...actual,
    Tree: (props: TreeProps) => {
      const draggable = props.draggable
      const enabled = typeof draggable === 'object'
        ? draggable.nodeDraggable?.({ key: 'a', title: 'A' }) !== false : !!draggable
      return (
        <div role="tree" aria-label={props.onDrop ? 'Pages tree' : 'Models tree'}>
          {props.onDrop && <button disabled={!enabled || props.disabled} onClick={() => props.onDrop?.({
            dragNode: { key: 'a' }, node: { key: 'target', pos: '0-1' }, dropPosition: 1, dropToGap: false,
          } as Parameters<NonNullable<TreeProps['onDrop']>>[0])}>Simulate drop</button>}
        </div>
      )
    },
  }
})

const page = (id: string, folderId: string, isTemplate = false) => ({
  id, folderId, spaceId: 's', parentId: null, title: id, slug: id, icon: null, orderIndex: 0,
  isFavorite: false, isArchived: false, isTemplate, createdAt: '2026-11-07T00:00:00Z', updatedAt: null,
})
const folders = ['source', 'target'].map((id) => ({
  id, spaceId: 's', parentId: null, name: id, icon: null, description: null, orderIndex: 0,
  createdAt: '2026-11-07T00:00:00Z', updatedAt: null,
}))
const props = {
  spaceId: 's', onSpaceChange: vi.fn(), selectedId: null, onSelect: vi.fn(),
  selectedFolderId: null, onSelectFolder: vi.fn(), onOpenOverview: vi.fn(), includeArchived: false,
  onToggleArchived: vi.fn(), tagIds: [] as string[], onTagIdsChange: vi.fn(), onSearch: vi.fn(),
}

beforeAll(async () => { await i18n.changeLanguage('en') })
beforeEach(() => {
  server.use(
    http.get(`${NOTES_BASE}/pages`, () => HttpResponse.json([page('a', 'source'), page('b', 'target'), page('model', 'target', true)])),
    http.get(`${NOTES_BASE}/folders`, () => HttpResponse.json(folders)),
    http.get(`${NOTES_BASE}/spaces`, () => HttpResponse.json([])),
    http.get(`${NOTES_BASE}/tags`, () => HttpResponse.json([])),
    http.get(`${NOTES_BASE}/pages/recent`, () => HttpResponse.json([])),
  )
})

describe('NotesSidebar tree drops', () => {
  it('saves the whole drop once, excluding models, and blocks new drops until it finishes', async () => {
    let sent: unknown
    let calls = 0
    let finish!: () => void
    const pending = new Promise<void>((resolve) => { finish = resolve })
    server.use(http.post(`${NOTES_BASE}/tree/move`, async ({ request }) => {
      calls++
      sent = await request.json()
      await pending
      return HttpResponse.json(true)
    }))
    renderWithProviders(<NotesSidebar {...props} />)
    const drop = screen.getByRole('button', { name: 'Simulate drop' })
    await waitFor(() => expect(drop).toBeEnabled())
    await userEvent.click(drop)
    await waitFor(() => expect(calls).toBe(1))
    expect(sent).toEqual({ spaceId: 's', kind: 'page', parentId: null, folderId: 'target', itemIds: ['b', 'a'] })
    expect(drop).toBeDisabled()
    finish()
    await waitFor(() => expect(drop).toBeEnabled())
    expect(screen.getByRole('tree', { name: 'Models tree' })).toBeInTheDocument()
  })

  it('refreshes after a failed move and allows a new attempt', async () => {
    let reads = 0
    server.use(
      http.get(`${NOTES_BASE}/pages`, () => {
        reads++
        return HttpResponse.json([page('a', 'source'), page('b', 'target')])
      }),
      http.post(`${NOTES_BASE}/tree/move`, () => HttpResponse.json({ success: false, errorMessage: 'The hierarchy changed.' }, { status: 409 })),
    )
    renderWithProviders(<NotesSidebar {...props} />)
    const drop = screen.getByRole('button', { name: 'Simulate drop' })
    await waitFor(() => expect(drop).toBeEnabled())
    await userEvent.click(drop)
    expect(await screen.findByText('The hierarchy changed.')).toBeInTheDocument()
    expect(reads).toBeGreaterThan(1)
    expect(drop).toBeEnabled()
  })

  it.each([
    { spaceId: null, tagIds: [] },
    { spaceId: 's', tagIds: ['tag'] },
  ])('disables drops outside a complete single-space view: %j', async (overrides) => {
    renderWithProviders(<NotesSidebar {...props} {...overrides} />)
    await screen.findByRole('tree', { name: 'Models tree' })
    expect(screen.getByRole('button', { name: 'Simulate drop' })).toBeDisabled()
  })

  it('disables dragging pages when their update permission is absent', async () => {
    const permission = vi.spyOn(access, 'usePermission').mockImplementation((key) => key !== 'notes.pages.update')
    renderWithProviders(<NotesSidebar {...props} />)
    await screen.findByRole('tree', { name: 'Models tree' })
    expect(screen.getByRole('button', { name: 'Simulate drop' })).toBeDisabled()
    permission.mockRestore()
  })
})
