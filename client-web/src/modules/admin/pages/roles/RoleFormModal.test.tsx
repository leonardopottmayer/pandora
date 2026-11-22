import { beforeAll, describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import i18n from '@/i18n'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { renderWithProviders } from '@/test/utils'
import type { CatalogModule, RoleDto } from '../../models'
import { RoleFormModal } from './RoleFormModal'

const BASE = `${TEST_API_BASE}/api/v1.0/admin`
const ok = (data: unknown) => HttpResponse.json({ success: true, data })

const action = (resource: string, name: string) => ({
  key: `finances.${resource}.${name}`,
  action: name,
  requires: name === 'read' ? [] : [`finances.${resource}.read`],
})

const catalog: CatalogModule[] = [
  {
    key: 'finances',
    resources: [
      { key: 'cards', feature: 'finances', actions: [action('cards', 'read'), action('cards', 'delete')] },
      { key: 'tags', feature: 'finances', actions: [action('tags', 'read')] },
    ],
  },
]

const role: RoleDto = {
  id: 'r1',
  name: 'Viewer',
  description: null,
  isSystem: false,
  isDefault: false,
  patterns: ['finances.cards.read'],
  holders: 2,
}

beforeAll(async () => {
  await i18n.changeLanguage('en')
})

describe('RoleFormModal', () => {
  it('saves the ticked permissions as the shortest patterns', async () => {
    let sent: unknown = null
    server.use(
      http.get(`${BASE}/catalog`, () => ok(catalog)),
      http.put(`${BASE}/roles/r1`, async ({ request }) => {
        sent = await request.json()
        return ok(true)
      }),
    )
    renderWithProviders(<RoleFormModal open role={role} onClose={() => {}} />)

    // Ticking the module ticks everything under it, so it is saved as one wildcard.
    await userEvent.click(await screen.findByRole('checkbox', { name: 'Finances' }))
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(sent).toEqual({ name: 'Viewer', description: null, patterns: ['finances.*'] }))
  })

  it('warns about an action held without its read', async () => {
    server.use(http.get(`${BASE}/catalog`, () => ok(catalog)))
    renderWithProviders(<RoleFormModal open role={{ ...role, patterns: ['finances.cards.delete'] }} onClose={() => {}} />)

    expect(await screen.findByText(/also need their resource's "View"/)).toBeInTheDocument()
    expect(screen.getByText('Finances · Cards · Delete')).toBeInTheDocument()
  })
})
