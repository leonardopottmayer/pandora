import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import i18n from '@/i18n'
import { NOTES_BASE } from '@/test/constants'
import { server } from '@/test/msw/server'
import { renderWithProviders } from '@/test/utils'
import type { FolderDto, PageSummaryDto } from '../models'
import type { SpaceNode } from '../lib/spaceTree'
import { FolderView } from './FolderView'

const folder: FolderDto = { id: 'folder', spaceId: 's', parentId: null, name: 'Work', icon: null, description: null, orderIndex: 0, createdAt: '2026-11-07T00:00:00Z', updatedAt: null }
const page: PageSummaryDto = { id: 'page', spaceId: 's', parentId: null, folderId: 'folder', title: 'A page', slug: 'a-page', icon: null, isArchived: false, isFavorite: false, isTemplate: false, orderIndex: 0, createdAt: '2026-11-07T00:00:00Z', updatedAt: null }
const node: SpaceNode = { kind: 'folder', id: folder.id, folder, children: [{ kind: 'page', id: page.id, page, children: [] }] }
const props = { folder, node, onOpen: vi.fn(), onCreatePage: vi.fn(), onCreateFolder: vi.fn(), onDeleted: vi.fn() }

beforeAll(async () => { await i18n.changeLanguage('en') })
beforeEach(() => localStorage.removeItem('pandora.notes_folder_view'))
afterEach(() => localStorage.removeItem('pandora.notes_folder_view'))

describe('FolderView', () => {
  it('keeps the existing view by default and requests properties only after selecting Table', async () => {
    let reads = 0
    server.use(http.get(`${NOTES_BASE}/folders/folder/properties`, () => {
      reads++
      return HttpResponse.json({ columns: ['Status'], pages: [{ page, properties: { Status: 'Done' } }] })
    }))
    renderWithProviders(<FolderView {...props} />)
    expect(reads).toBe(0)
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('radio', { name: /Table/ }).closest('label')!)
    expect(await screen.findByText('Done')).toBeInTheDocument()
    expect(reads).toBe(1)
    expect(localStorage.getItem('pandora.notes_folder_view')).toBe('table')
    await userEvent.click(screen.getByRole('radio', { name: /List/ }).closest('label')!)
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('restores a chosen table view and lets an empty folder return to the existing view', async () => {
    localStorage.setItem('pandora.notes_folder_view', 'table')
    server.use(http.get(`${NOTES_BASE}/folders/folder/properties`, () => HttpResponse.json({ columns: [], pages: [] })))
    renderWithProviders(<FolderView {...props} node={{ ...node, children: [] }} />)
    expect(await screen.findByText('No pages directly in this folder.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('radio', { name: /List/ }).closest('label')!)
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(localStorage.getItem('pandora.notes_folder_view')).toBe('list')
  })
})
