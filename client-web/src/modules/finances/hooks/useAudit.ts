import { useQuery } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import * as auditService from '../services/audit.service'
import type { AuditTimelineParams } from '../services/audit.service'

export function useAuditTimeline(params: AuditTimelineParams = {}, enabled = true) {
  return useQuery({
    queryKey: financeKeys.auditTimeline(params),
    queryFn: () => auditService.listAuditTimeline(params),
    // The backend requires (entityType + entityId) OR correlationId; only queries when valid.
    enabled,
  })
}

/** Complete timeline for the current filter; kept separate from paged query results. */
export function useAllAuditTimeline(params: Omit<AuditTimelineParams, 'skip' | 'take'>, enabled: boolean) {
  return useQuery({
    queryKey: financeKeys.auditTimeline({ ...params, take: -1 }),
    queryFn: () => auditService.listAllAuditTimeline(params),
    enabled,
  })
}
