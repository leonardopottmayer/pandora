import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { AGENDA_BASE, TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { TaskListBindingDto } from '../../models'
import { GoogleTasksSection } from './GoogleTasksSection'

const binding: TaskListBindingDto = {
  id: 'b1',
  listId: 'l1',
  listName: 'Compras',
  externalAccountId: 'a1',
  remoteListId: 'L1',
  status: 'needs-reconnect',
  lastSyncedAt: null,
  lastError: 'The Google account was connected without access to its tasks.',
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('GoogleTasksSection', () => {
  it('lists the linked task lists with their status and why they stopped', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/google/task-list-bindings`, () => HttpResponse.json({ success: true, data: [binding] })),
      http.get(`${TEST_API_BASE}/api/v1.0/integrations/accounts`, () => HttpResponse.json({ success: true, data: [] })),
    )
    renderWithProviders(<GoogleTasksSection />)

    expect(await screen.findByText('Compras')).toBeInTheDocument()
    expect(screen.getByText('Reconnect the account')).toBeInTheDocument()
    expect(screen.getByText(/without access to its tasks/)).toBeInTheDocument()
    expect(screen.getByText('Add task lists')).toBeInTheDocument()
  })
})
