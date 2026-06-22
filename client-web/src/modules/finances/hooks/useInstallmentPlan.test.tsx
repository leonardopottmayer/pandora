import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { createHookWrapper } from '@/test/utils'
import { useInstallmentPlan } from './useInstallmentPlan'
import * as installmentPlansService from '../services/installmentPlans.service'

vi.mock('../services/installmentPlans.service')

beforeEach(() => vi.clearAllMocks())

describe('useInstallmentPlan', () => {
  it('does not fetch with an empty id', () => {
    const { wrapper } = createHookWrapper()
    const { result } = renderHook(() => useInstallmentPlan(''), { wrapper })
    expect(result.current.fetchStatus).toBe('idle')
    expect(installmentPlansService.getInstallmentPlan).not.toHaveBeenCalled()
  })

  it('fetches the plan when an id is provided', async () => {
    vi.mocked(installmentPlansService.getInstallmentPlan).mockResolvedValue({ id: 'plan1' } as never)
    const { wrapper } = createHookWrapper()
    const { result } = renderHook(() => useInstallmentPlan('plan1'), { wrapper })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(installmentPlansService.getInstallmentPlan).toHaveBeenCalledWith('plan1')
  })
})
