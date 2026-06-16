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
