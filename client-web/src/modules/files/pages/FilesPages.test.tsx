import { describe, it, expect, beforeAll, afterEach, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import type { PandoraDesktop } from '@/lib/desktop'
import { server } from '@/test/msw/server'
import { FILES_BASE, TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { Entry, ReviewNode, Root, Scan } from '../models'
import { CatalogPage } from './CatalogPage'
import { FilesSettingsPage } from './FilesSettingsPage'
import { ReviewPage } from './ReviewPage'
import { RootsPage } from './RootsPage'

const ok = (data: unknown) => HttpResponse.json({ success: true, data })

const HOMELAB = 'd-homelab'

function root(overrides: Partial<Root> = {}): Root {
  return {
    id: 'r1',
    deviceId: HOMELAB,
    name: 'Disk 2',
    localPath: 'E:\\',
    caseSensitive: false,
    includeHidden: false,
    scanTime: '03:00:00',
    status: 'active',
    lastCompletedScanAt: null,
    entryCount: 0,
    marks: [],
    ...overrides,
  }
}

function entry(overrides: Partial<Entry>): Entry {
  return {
    id: 'e1',
    rootId: 'r1',
    kind: 'file',
    relativePath: '/Series/Breaking Bad S02E01.mkv',
    parentPath: '/Series',
    name: 'Breaking Bad S02E01.mkv',
    extension: 'mkv',
    category: 'video',
    sizeBytes: 1536,
    modifiedAt: null,
    metadata: null,
    status: 'present',
    missingSince: null,
    keptAt: null,
    firstSeenAt: '2026-10-08T00:00:00Z',
    ...overrides,
  }
}

/** The account switch on, the homelab paired, and the given roots. */
function filesApi(roots: Root[], scans: Scan[] = []) {
  server.use(
    http.get(`${FILES_BASE}/preferences`, () => ok({ isEnabled: true })),
    http.get(`${FILES_BASE}/roots`, () => ok(roots)),
    http.get(`${FILES_BASE}/scans`, () => ok(scans)),
    http.get(`${TEST_API_BASE}/api/v1/identity/devices`, () =>
      ok([{ id: HOMELAB, name: 'Homelab', platform: 'windows', form: 'desktop', scopes: [], createdAt: '', lastSeenAt: null }]),
    ),
  )
}

/** Pandora Desktop on the homelab, with the Files module on. */
function onHomelab(modules = ['files']) {
  const invoke = vi.fn(async (method: string) => {
    switch (method) {
      case 'desktop.getSettings':
        return { deviceId: HOMELAB }
      case 'desktop.getModules':
        return [{ name: 'files', enabled: modules.includes('files') }]
      case 'desktop.setModule':
        return { restarting: true }
      case 'files.status':
        return { paired: true, enabled: true, roots: [{ id: 'r1' }], running: null, lastError: null }
      default:
        return true
    }
  })
  window.pandoraDesktop = {
    version: '0.1.0',
    capabilities: async () => modules,
    invoke: invoke as PandoraDesktop['invoke'],
    on: () => () => {},
  }
  return invoke
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

afterEach(() => {
  delete window.pandoraDesktop
})

describe('Files pages', () => {
  it('turns the account switch on, and this PC scanning on through the bridge', async () => {
    let saved: unknown = null
    server.use(
      http.get(`${FILES_BASE}/preferences`, () => ok({ isEnabled: false })),
      http.put(`${FILES_BASE}/preferences`, async ({ request }) => {
        saved = await request.json()
        return ok(null)
      }),
    )
    const invoke = onHomelab([])
    renderWithProviders(<FilesSettingsPage />)

    const [account, device] = await screen.findAllByRole('switch')
    await waitFor(() => expect(account).not.toHaveClass('ant-switch-loading'))
    await userEvent.click(account)
    await waitFor(() => expect(saved).toEqual({ isEnabled: true }))

    await waitFor(() => expect(device).not.toHaveClass('ant-switch-loading'))
    await userEvent.click(device)
    expect(invoke).toHaveBeenCalledWith('desktop.setModule', { name: 'files', enabled: true })
  })

  it('points to the settings while Files is off', async () => {
    server.use(http.get(`${FILES_BASE}/preferences`, () => ok({ isEnabled: false })))
    renderWithProviders(<CatalogPage />)
    expect(await screen.findByText('Files is off')).toBeInTheDocument()
  })

  it('offers "Scan now" on the PC that has the root, and applies a held scan', async () => {
    let resolved = ''
    filesApi(
      [root()],
      [
        {
          id: 's1', rootId: 'r1', status: 'held', startedAt: '2026-10-08T03:00:00Z', lastBatchAt: null, finishedAt: null,
          seen: 10, created: 0, changed: 0, moved: 0, missing: 9, excluded: 0, error: null,
        },
      ],
    )
    server.use(
      http.post(`${FILES_BASE}/scans/s1/confirm`, () => {
        resolved = 'confirm'
        return ok(null)
      }),
    )
    const invoke = onHomelab()
    renderWithProviders(<RootsPage />)

    expect(await screen.findByText('The last scan would mark 9 of 10 entries missing')).toBeInTheDocument()
    await userEvent.click(await screen.findByRole('button', { name: /Scan now/ }))
    expect(invoke).toHaveBeenCalledWith('files.scanNow', { rootId: 'r1' })

    await userEvent.click(screen.getByRole('button', { name: 'Apply' }))
    await waitFor(() => expect(resolved).toBe('confirm'))
  })

  it('has no "Scan now" in a browser, and saves the folders picked from the catalog', async () => {
    let marks: unknown = null
    filesApi([root()])
    server.use(
      http.get(`${FILES_BASE}/roots/r1/entries`, ({ request }) =>
        ok({
          items:
            new URL(request.url).searchParams.get('parentPath') === '/'
              ? [entry({ id: 'f1', kind: 'directory', relativePath: '/C', parentPath: '/', name: 'C' })]
              : [],
          hasMore: false,
        }),
      ),
      http.put(`${FILES_BASE}/roots/r1/selection`, async ({ request }) => {
        marks = await request.json()
        return ok(root())
      }),
    )
    renderWithProviders(<RootsPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Folders' }))
    expect(screen.queryByRole('button', { name: /Scan now/ })).not.toBeInTheDocument()

    await userEvent.click(await screen.findByRole('checkbox', { name: 'C' }))
    const drawer = screen.getByRole('dialog')
    expect(within(drawer).getByText('/C')).toBeInTheDocument()
    await userEvent.click(within(drawer).getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(marks).toEqual({ marks: [{ path: '/C', mode: 'exclude' }] }))
  })

  it('finds a file by a fragment of its name, and copies where it is', async () => {
    let query: string | null = null
    filesApi([root()])
    server.use(
      http.get(`${FILES_BASE}/roots/r1/entries`, () => ok({ items: [], hasMore: false })),
      http.get(`${FILES_BASE}/search`, ({ request }) => {
        query = new URL(request.url).searchParams.get('q')
        return ok({ items: [entry({})], hasMore: false })
      }),
    )
    renderWithProviders(<CatalogPage />)

    await userEvent.type(await screen.findByPlaceholderText(/Search by name/), 'breaking s02{Enter}')

    expect(await screen.findByText('Breaking Bad S02E01.mkv')).toBeInTheDocument()
    expect(query).toBe('breaking s02')
    expect(screen.getByText('Disk 2 /Series')).toBeInTheDocument()
    expect(screen.getByText('1.5 KB')).toBeInTheDocument()
  })

  it('narrows videos by resolution, and shows what each file says', async () => {
    let params = new URLSearchParams()
    filesApi([root()])
    server.use(
      http.get(`${FILES_BASE}/roots/r1/entries`, () => ok({ items: [], hasMore: false })),
      http.get(`${FILES_BASE}/search`, ({ request }) => {
        params = new URL(request.url).searchParams
        return ok({
          items: [entry({ metadata: { width: 3840, height: 2160, durationSeconds: 7200, latitude: -27.6, longitude: -48.5 } })],
          hasMore: false,
        })
      }),
    )
    renderWithProviders(<CatalogPage />)

    // Root, type and status; picking Video adds the resolution.
    await waitFor(() => expect(screen.getAllByRole('combobox')).toHaveLength(3))
    await userEvent.click(screen.getAllByRole('combobox')[1])
    await userEvent.click(await screen.findByText('Video'))
    await userEvent.click(screen.getAllByRole('combobox')[2])
    await userEvent.click(await screen.findByText('4K (2160p and up)'))

    expect(await screen.findByText(/3840×2160 · 2:00:00/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Map' })).toHaveAttribute('href', expect.stringContaining('mlat=-27.6&mlon=-48.5'))
    await waitFor(() => expect(params.get('resolution')).toBe('4k'))
    expect(params.get('category')).toBe('video')
  })

  it('reviews a whole folder at once', async () => {
    let decision: unknown = null
    const node = (o: Partial<ReviewNode>): ReviewNode => ({
      rootId: 'r1', name: 'Disk 2', path: '/', count: 3, hasChildren: true, entryId: null, status: null, kind: null, ...o,
    })
    server.use(
      http.get(`${FILES_BASE}/preferences`, () => ok({ isEnabled: true })),
      http.get(`${FILES_BASE}/review/tree`, ({ request }) =>
        ok(
          new URL(request.url).searchParams.get('rootId')
            ? [node({ name: 'Old', path: '/Old', kind: 'directory', status: 'missing', entryId: 'e9' })]
            : [node({})],
        ),
      ),
      http.post(`${FILES_BASE}/review/keep`, async ({ request }) => {
        decision = await request.json()
        return ok(3)
      }),
    )
    renderWithProviders(<ReviewPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Expand' }))
    const row = (await screen.findByText('Old')).closest('div.flex-1') as HTMLElement
    await userEvent.click(within(row).getByRole('button', { name: 'Keep' }))
    await waitFor(() => expect(decision).toEqual({ entryIds: [], folders: [{ rootId: 'r1', path: '/Old' }] }))
  })
})
