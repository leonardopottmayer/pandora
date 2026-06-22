import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { FINANCES_BASE } from '@/test/constants'
import { getInstallmentPlan } from './installmentPlans.service'
import type { InstallmentPlanDto } from '../models'

const plan: InstallmentPlanDto = {
  id: 'plan1',
  cardId: 'c1',
  origin: 'manual',
  description: 'Geladeira 12x',
  installmentCount: 12,
  totalAmount: 1200,
  totalIsEstimate: false,
  firstReferenceMonth: '2026-06',
  remainingAmount: 1000,
  paidInstallments: 2,
  systemCategoryId: null,
  userCategoryId: null,
  installments: [],
}

describe('installmentPlans.service', () => {
  it('fetches an installment plan by id', async () => {
    server.use(
      http.get(`${FINANCES_BASE}/installment-plans/plan1`, () =>
        HttpResponse.json({ success: true, data: plan }),
      ),
    )
    const result = await getInstallmentPlan('plan1')
    expect(result.id).toBe('plan1')
    expect(result.installmentCount).toBe(12)
  })
})
