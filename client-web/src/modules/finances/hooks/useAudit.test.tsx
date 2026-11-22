import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { createHookWrapper } from '@/test/utils'
import { useAllAuditTimeline, useAuditTimeline } from './useAudit'
import * as auditService from '../services/audit.service'

vi.mock('../services/audit.service')

beforeEach(() => vi.clearAllMocks())

describe('useAuditTimeline', () => {
  it('keeps the complete timeline disabled until a filter is ready', () => {
    const { wrapper } = createHookWrapper()
    const { result } = renderHook(() => useAllAuditTimeline({}, false), { wrapper })
    expect(result.current.fetchStatus).toBe('idle')
    expect(auditService.listAllAuditTimeline).not.toHaveBeenCalled()
  })

  it('loads the complete timeline through its separate query', async () => {
    vi.mocked(auditService.listAllAuditTimeline).mockResolvedValue([])
    const { wrapper } = createHookWrapper()
    const params = { entityType: 'account', entityId: 'a1' }
    const { result } = renderHook(() => useAllAuditTimeline(params, true), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(auditService.listAllAuditTimeline).toHaveBeenCalledWith(params)
    expect(auditService.listAuditTimeline).not.toHaveBeenCalled()
  })
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
