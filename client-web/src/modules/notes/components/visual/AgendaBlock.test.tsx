import { useState } from 'react'
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import i18n from '@/i18n'
import { AGENDA_BASE } from '@/test/constants'
import { server } from '@/test/msw/server'
import { createTestQueryClient, renderWithProviders } from '@/test/utils'
import * as access from '@/modules/identity/hooks/useAccess'
import { agendaKeys } from '@/modules/agenda/hooks/queryKeys'
import { useCompleteTask } from '@/modules/agenda/hooks/useTasks'
import { VisualEditorContext, type VisualEditorContextValue } from './visualContext'
import { AgendaBlock } from './AgendaBlock'

const context: VisualEditorContextValue = { pageId: 'page', editable: true, pagesById: new Map(), foldersById: new Map(), onOpenPage: vi.fn(), pickMedia: async () => null }
const list = { id: 'work', name: 'Work', isDefault: false, position: 0, archivedAt: null }
const task = { id: 'task', listId: 'work', title: 'Review proposal', status: 'Todo', priority: 'None', position: 0, timeZone: 'America/Sao_Paulo', dueHasTime: false }
const dayItem = { kind: 'event', id: 'event', title: 'Planning', at: '2026-11-08T01:30:00Z', isAllDay: false }

beforeAll(async () => { await i18n.changeLanguage('en') })
afterEach(() => vi.restoreAllMocks())

describe('AgendaBlock', () => {
  it('reads the existing account-day endpoint, shows all kinds in the account zone and never saves fetched values', async () => {
    const update = vi.fn()
    let queryString: string | undefined
    server.use(http.get(`${AGENDA_BASE}/today`, ({ request }) => {
      queryString = new URL(request.url).search
      return HttpResponse.json([dayItem,
        { ...dayItem, kind: 'task', id: 'task', title: 'Send proposal', isAllDay: true, status: 'Done' },
        { ...dayItem, kind: 'reminder', id: 'reminder', title: '<img src=x onerror=alert(1)>' },
      ])
    }))
    const { container } = renderWithProviders(<VisualEditorContext.Provider value={context}>
      <AgendaBlock props={{}} editable update={update} />
    </VisualEditorContext.Provider>)
    await screen.findByText('Planning')
    expect(screen.getByText('Send proposal')).toBeInTheDocument()
    expect(screen.getByText('Done')).toBeInTheDocument()
    expect(screen.getAllByText(/22:30/)).toHaveLength(2)
    expect(screen.getByText('All day')).toBeInTheDocument()
    expect(screen.getByText('<img src=x onerror=alert(1)>')).toBeInTheDocument()
    expect(container.querySelector('img')).toBeNull()
    expect(queryString).toBe('')
    expect(update).not.toHaveBeenCalled()
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Open in Agenda' })).toHaveAttribute('href', '/agenda')
  })

  it('keeps public content private even when the owner data is already cached, without querying Agenda', () => {
    const client = createTestQueryClient()
    client.setQueryDefaults(agendaKeys.all, { gcTime: Infinity, staleTime: Infinity })
    client.setQueryData(agendaKeys.today(), [dayItem])
    client.setQueryData(agendaKeys.taskListList(), [list])
    client.setQueryData(agendaKeys.taskList({ listId: 'work' }), [task])
    let calls = 0
    server.use(http.get(`${AGENDA_BASE}/*`, () => { calls++; return HttpResponse.json([]) }))
    renderWithProviders(<VisualEditorContext.Provider value={{ ...context, publicView: true }}>
      <AgendaBlock props={{ mode: 'tasks', listId: 'work' }} editable={false} update={vi.fn()} />
    </VisualEditorContext.Provider>, { queryClient: client })
    expect(screen.getByText('Private content')).toBeInTheDocument()
    expect(screen.queryByText('Review proposal')).not.toBeInTheDocument()
    expect(screen.queryByText('Work')).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
    expect(calls).toBe(0)
  })

  it.each(['feature', 'permission'])('does not mount Agenda queries without %s access', (denied) => {
    if (denied === 'feature') vi.spyOn(access, 'useFeature').mockReturnValue(false)
    else vi.spyOn(access, 'usePermission').mockReturnValue(false)
    let calls = 0
    server.use(http.get(`${AGENDA_BASE}/*`, () => { calls++; return HttpResponse.json([]) }))
    renderWithProviders(<VisualEditorContext.Provider value={context}><AgendaBlock props={{}} editable update={vi.fn()} /></VisualEditorContext.Provider>)
    expect(screen.getByText('Agenda content is unavailable with your current access.')).toBeInTheDocument()
    expect(calls).toBe(0)
  })

  it.each([
    ['today', 'agenda.tasks.read'], ['today', 'agenda.events.read'],
    ['today', 'agenda.reminders.read'], ['tasks', 'agenda.lists.read'],
  ])('does not query source %s when %s is missing', (mode, permission) => {
    vi.spyOn(access, 'usePermission').mockImplementation((key) => key !== permission)
    let calls = 0
    server.use(http.get(`${AGENDA_BASE}/*`, () => { calls++; return HttpResponse.json([]) }))
    renderWithProviders(<VisualEditorContext.Provider value={context}><AgendaBlock props={{ mode, listId: 'work' }} editable update={vi.fn()} /></VisualEditorContext.Provider>)
    expect(screen.getByText('Agenda content is unavailable with your current access.')).toBeInTheDocument()
    expect(calls).toBe(0)
  })

  it('loads only the configured active list and displays pending tasks in read mode', async () => {
    let requested: string | null = null
    server.use(
      http.get(`${AGENDA_BASE}/task-lists`, () => HttpResponse.json([list])),
      http.get(`${AGENDA_BASE}/tasks`, ({ request }) => {
        requested = new URL(request.url).searchParams.get('listId')
        return HttpResponse.json([task, { ...task, id: 'in-progress', status: 'InProgress', title: 'Write tests' },
          { ...task, id: 'done', status: 'Done', title: 'Finished item' }, { ...task, id: 'cancelled', status: 'Cancelled', title: 'Cancelled item' }])
      }),
    )
    const update = vi.fn()
    renderWithProviders(<VisualEditorContext.Provider value={context}><AgendaBlock props={{ mode: 'tasks', listId: 'work' }} editable={false} update={update} /></VisualEditorContext.Provider>)
    await screen.findByText('Review proposal')
    expect(screen.getByText('Write tests')).toBeInTheDocument()
    expect(screen.queryByText('Finished item')).not.toBeInTheDocument()
    expect(screen.queryByText('Cancelled item')).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    expect(requested).toBe('work')
    expect(update).not.toHaveBeenCalled()
  })

  it.each([null, 'missing', 'archived'])('does not fall back to all tasks for an unconfigured/unavailable list: %s', async (listId) => {
    let calls = 0
    server.use(
      http.get(`${AGENDA_BASE}/task-lists`, () => HttpResponse.json([{ ...list, id: 'archived', archivedAt: '2026-11-07T00:00:00Z' }])),
      http.get(`${AGENDA_BASE}/tasks`, () => { calls++; return HttpResponse.json([task]) }),
    )
    renderWithProviders(<VisualEditorContext.Provider value={context}><AgendaBlock props={{ mode: 'tasks', listId }} editable={false} update={vi.fn()} /></VisualEditorContext.Provider>)
    await screen.findByText(listId ? 'This list is unavailable. Choose another list while editing the note.' : 'Choose a task list.')
    expect(calls).toBe(0)
    expect(screen.queryByText('Review proposal')).not.toBeInTheDocument()
  })

  it('persists only source and list configuration while using current list names and task data', async () => {
    server.use(
      http.get(`${AGENDA_BASE}/today`, () => HttpResponse.json([])),
      http.get(`${AGENDA_BASE}/task-lists`, () => HttpResponse.json([list])),
      http.get(`${AGENDA_BASE}/tasks`, () => HttpResponse.json([task])),
    )
    const saved = vi.fn()
    function EditableBlock() {
      const [props, setProps] = useState<Record<string, unknown>>({})
      return <AgendaBlock props={props} editable update={(next) => { saved(next); setProps(next) }} />
    }
    renderWithProviders(<VisualEditorContext.Provider value={context}><EditableBlock /></VisualEditorContext.Provider>)
    await userEvent.click(screen.getByRole('combobox', { name: 'Agenda source' }))
    await userEvent.click(await screen.findByText('Tasks from a list'))
    await userEvent.click(await screen.findByRole('combobox', { name: 'Task list' }))
    await userEvent.click(await screen.findByText('Work'))
    await screen.findByText('Review proposal')
    expect(saved.mock.calls.map(([value]) => value)).toEqual([{ mode: 'tasks' }, { mode: 'tasks', listId: 'work' }])
  })

  it('refreshes current data without changing the note configuration', async () => {
    let reads = 0
    const update = vi.fn()
    server.use(http.get(`${AGENDA_BASE}/today`, () => HttpResponse.json([{ ...dayItem, title: ++reads === 1 ? 'Before' : 'After' }])))
    renderWithProviders(<VisualEditorContext.Provider value={context}><AgendaBlock props={{}} editable={false} update={update} /></VisualEditorContext.Provider>)
    await screen.findByText('Before')
    await userEvent.click(screen.getByRole('button', { name: 'Refresh Agenda' }))
    await screen.findByText('After')
    expect(reads).toBe(2)
    expect(update).not.toHaveBeenCalled()
  })

  it('reacts to normal Agenda task mutations through the shared query keys', async () => {
    let done = false
    server.use(
      http.get(`${AGENDA_BASE}/task-lists`, () => HttpResponse.json([list])),
      http.get(`${AGENDA_BASE}/tasks`, () => HttpResponse.json([{ ...task, status: done ? 'Done' : 'Todo' }])),
      http.post(`${AGENDA_BASE}/tasks/task/complete`, () => { done = true; return HttpResponse.json({ ...task, status: 'Done' }) }),
    )
    function CompleteInAgenda() {
      const complete = useCompleteTask()
      return <button onClick={() => complete.mutate('task')}>Complete in Agenda</button>
    }
    const update = vi.fn()
    renderWithProviders(<VisualEditorContext.Provider value={context}>
      <AgendaBlock props={{ mode: 'tasks', listId: 'work' }} editable={false} update={update} /><CompleteInAgenda />
    </VisualEditorContext.Provider>)
    await screen.findByText('Review proposal')
    await userEvent.click(screen.getByRole('button', { name: 'Complete in Agenda' }))
    await screen.findByText('No pending tasks in this list.')
    expect(screen.queryByText('Review proposal')).not.toBeInTheDocument()
    expect(update).not.toHaveBeenCalled()
  })

  it('hides stale results after an API error and supports retry through Refresh', async () => {
    const client = createTestQueryClient()
    client.setQueryData(agendaKeys.today(), [dayItem])
    server.use(http.get(`${AGENDA_BASE}/today`, () => HttpResponse.json({ success: false, errorMessage: 'Agenda refused.' }, { status: 403 })))
    renderWithProviders(<VisualEditorContext.Provider value={context}><AgendaBlock props={{}} editable={false} update={vi.fn()} /></VisualEditorContext.Provider>, { queryClient: client })
    expect(await screen.findByRole('alert')).toHaveTextContent('Agenda refused.')
    expect(screen.queryByText('Planning')).not.toBeInTheDocument()
    server.use(http.get(`${AGENDA_BASE}/today`, () => HttpResponse.json([{ ...dayItem, title: 'Recovered' }])))
    await userEvent.click(screen.getByRole('button', { name: 'Refresh Agenda' }))
    await screen.findByText('Recovered')
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument())
  })
})
