import { beforeAll, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import i18n from '@/i18n'
import { renderWithProviders } from '@/test/utils'
import type { AuditEventDto } from '../../models'
import { AuditEventDrawer } from './AuditEventDrawer'

const event: AuditEventDto = {
  id: 'event-1', actorUserId: 'user-1', entityType: 'account', entityId: 'account-1',
  eventType: 'account.created', occurredAt: '2026-06-13T10:00:00Z',
  correlationId: 'correlation-1', data: '{"name":"Checking","amount":123}',
}

beforeAll(async () => { await i18n.changeLanguage('en') })

describe('AuditEventDrawer', () => {
  it('shows full identifiers and formats the recorded JSON', () => {
    renderWithProviders(<AuditEventDrawer event={event} onClose={vi.fn()} />)
    expect(screen.getByText('Account created')).toBeInTheDocument()
    for (const value of ['event-1', 'user-1', 'account-1', 'account.created', 'correlation-1']) {
      expect(screen.getByText(value)).toBeInTheDocument()
    }
    expect(screen.getByText(/"amount": 123/).textContent).toBe(JSON.stringify(JSON.parse(event.data!), null, 2))
  })

  it('preserves unknown event codes and non-JSON payloads without losing the data', () => {
    renderWithProviders(<AuditEventDrawer event={{ ...event, eventType: 'future.event', data: 'Original text, not JSON' }} onClose={vi.fn()} />)
    expect(screen.getAllByText('future.event')).toHaveLength(2)
    expect(screen.getByText('Original text, not JSON')).toBeInTheDocument()
  })

  it('explains when no additional data was recorded', () => {
    renderWithProviders(<AuditEventDrawer event={{ ...event, data: null, actorUserId: null, correlationId: null }} onClose={vi.fn()} />)
    expect(screen.getByText('No additional data was recorded.')).toBeInTheDocument()
    expect(screen.getAllByText('—')).toHaveLength(2)
  })
})
