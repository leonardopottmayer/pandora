import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { AGENDA_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { TaskDto, TaskListDto } from '../../models'
import { TasksListPage } from './TasksListPage'

const list: TaskListDto = { id: 'l1', name: 'Inbox', isDefault: true, position: 0, archivedAt: null }

function task(overrides: Partial<TaskDto>): TaskDto {
  return {
    id: 'task-1',
    listId: 'l1',
    parentTaskId: null,
    title: 'Write report',
    notes: null,
    dueAt: null,
    dueHasTime: false,
    priority: 'None',
    status: 'Todo',
    completedAt: null,
    timeZone: 'America/Sao_Paulo',
    rrule: null,
    position: 0,
    ...overrides,
  }
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

function mockLists() {
  server.use(
    http.get(`${AGENDA_BASE}/task-lists`, () => HttpResponse.json({ success: true, data: [list] })),
  )
}

describe('TasksListPage', () => {
  it('renders tasks grouped under buckets', async () => {
    mockLists()
    server.use(
      http.get(`${AGENDA_BASE}/tasks`, () =>
        HttpResponse.json({ success: true, data: [task({})] }),
      ),
    )
    renderWithProviders(<TasksListPage />)
    expect(await screen.findByText('Write report')).toBeInTheDocument()
    // No due date → "No date" bucket.
    expect(screen.getByText('No date · 1')).toBeInTheDocument()
  })

  it('keeps a done task out of overdue, under the completed tasks', async () => {
    mockLists()
    server.use(
      http.get(`${AGENDA_BASE}/tasks`, () =>
        HttpResponse.json({
          success: true,
          data: [
            task({ id: 'late', title: 'Pay the bill', dueAt: '2020-01-01T12:00:00Z' }),
            task({ id: 'done', title: 'Old chore', dueAt: '2020-01-01T12:00:00Z', status: 'Done' }),
          ],
        }),
      ),
    )
    renderWithProviders(<TasksListPage />)

    expect(await screen.findByText('Overdue · 1')).toBeInTheDocument()
    expect(screen.getByText('Completed · 1')).toBeInTheDocument()
  })

  it('adds a task to the default list from the quick add field', async () => {
    mockLists()
    const bodies: unknown[] = []
    server.use(
      http.get(`${AGENDA_BASE}/tasks`, () => HttpResponse.json({ success: true, data: [] })),
      http.post(`${AGENDA_BASE}/tasks`, async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json({ success: true, data: task({}) })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<TasksListPage />)

    const input = await screen.findByRole('textbox', { name: 'Add a task and press Enter' })
    await waitFor(() => expect(input).toBeEnabled())
    await user.type(input, 'Buy milk{Enter}')

    await waitFor(() => expect(bodies).toEqual([expect.objectContaining({ listId: 'l1', title: 'Buy milk' })]))
    await waitFor(() => expect(input).toHaveValue(''))
  })

  it('deletes from the menu after confirming', async () => {
    mockLists()
    let deleted = false
    server.use(
      http.get(`${AGENDA_BASE}/tasks`, () => HttpResponse.json({ success: true, data: [task({})] })),
      http.delete(`${AGENDA_BASE}/tasks/task-1`, () => {
        deleted = true
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<TasksListPage />)

    await user.click(await screen.findByRole('button', { name: 'More actions for Write report' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Delete' }))
    expect(deleted).toBe(false)

    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Delete' }))
    await waitFor(() => expect(deleted).toBe(true))
  })

  it('nests subtasks under their parent', async () => {
    mockLists()
    server.use(
      http.get(`${AGENDA_BASE}/tasks`, () =>
        HttpResponse.json({
          success: true,
          data: [task({}), task({ id: 'task-2', parentTaskId: 'task-1', title: 'Sub step' })],
        }),
      ),
    )
    renderWithProviders(<TasksListPage />)
    expect(await screen.findByText('Write report')).toBeInTheDocument()
    expect(screen.getByText('Sub step')).toBeInTheDocument()
  })

  it('completes a task via the checkbox', async () => {
    mockLists()
    let completed = false
    server.use(
      http.get(`${AGENDA_BASE}/tasks`, () =>
        HttpResponse.json({ success: true, data: [task({})] }),
      ),
      http.post(`${AGENDA_BASE}/tasks/task-1/complete`, () => {
        completed = true
        return HttpResponse.json({ success: true, data: task({ status: 'Done' }) })
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<TasksListPage />)

    await screen.findByText('Write report')
    await user.click(screen.getByRole('checkbox'))
    await waitFor(() => expect(completed).toBe(true))
  })
})
