import { apiClient } from '@/lib/api/client'
import { fetchAllPages } from './transactions.service'
import type { AuditEventDto } from '../models'

const BASE = '/api/v1.0/finances/audit'

export interface AuditTimelineParams {
  entityType?: string
  entityId?: string
  correlationId?: string
  skip?: number
  take?: number
}

export async function listAuditTimeline(
  params: AuditTimelineParams = {},
): Promise<AuditEventDto[]> {
  const { data } = await apiClient.get<AuditEventDto[]>(BASE, {
    params: {
      entityType: params.entityType,
      entityId: params.entityId,
      correlationId: params.correlationId,
      skip: params.skip,
      take: params.take,
    },
  })
  return data
}

/** Every event matching an entity or correlation, across all API pages. */
export function listAllAuditTimeline(params: Omit<AuditTimelineParams, 'skip' | 'take'>): Promise<AuditEventDto[]> {
  return fetchAllPages((skip, take) => listAuditTimeline({ ...params, skip, take }))
}
