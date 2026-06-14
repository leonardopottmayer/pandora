import { apiClient } from '@/lib/api/client'
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
