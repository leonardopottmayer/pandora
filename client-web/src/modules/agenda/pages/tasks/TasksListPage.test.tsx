import { describe, it, expect, beforeAll } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
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
    expect(screen.getByText('No date')).toBeInTheDocument()
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
