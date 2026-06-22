import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { createHookWrapper } from '@/test/utils'
import { useAuditTimeline } from './useAudit'
import * as auditService from '../services/audit.service'

vi.mock('../services/audit.service')

beforeEach(() => vi.clearAllMocks())

describe('useAuditTimeline', () => {
  it('does not fetch when explicitly disabled (invalid filter combination)', () => {
    const { wrapper } = createHookWrapper()
    const { result } = renderHook(() => useAuditTimeline({}, false), { wrapper })
    expect(result.current.fetchStatus).toBe('idle')
    expect(auditService.listAuditTimeline).not.toHaveBeenCalled()
  })

  it('fetches when enabled, forwarding the params', async () => {
    vi.mocked(auditService.listAuditTimeline).mockResolvedValue([] as never)
    const { wrapper } = createHookWrapper()
    const params = { entityType: 'transaction', entityId: 'x1' }
    const { result } = renderHook(() => useAuditTimeline(params, true), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(auditService.listAuditTimeline).toHaveBeenCalledWith(params)
  })
})
