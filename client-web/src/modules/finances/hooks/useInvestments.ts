import { useMemo } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import type { AddPositionMovementRequest, AssetBehavior, CreateAssetRequest, CreatePositionRequest } from '../models'
import * as service from '../services/investments.service'
import type { ListPositionsParams } from '../services/investments.service'

export function useAssetClasses() {
  return useQuery({ queryKey: financeKeys.assetClasses(), queryFn: service.listAssetClasses })
}

export function useAssets() {
  return useQuery({ queryKey: financeKeys.assets(), queryFn: service.listAssets })
}

export function usePositions(params: ListPositionsParams = {}) {
  return useQuery({ queryKey: financeKeys.positionList(params), queryFn: () => service.listPositions(params) })
}

export function usePosition(id: string | null) {
  return useQuery({
    queryKey: financeKeys.position(id ?? ''),
    queryFn: () => service.getPosition(id!),
    enabled: !!id,
  })
}

export function useInvestmentsSummary() {
  return useQuery({ queryKey: financeKeys.investmentsSummary(), queryFn: service.getInvestmentsSummary })
}

/** id → class map, for labels. */
export function useAssetClassMap() {
  const { data } = useAssetClasses()
  return useMemo(() => new Map((data ?? []).map((c) => [c.id, c])), [data])
}

/**
 * A movement can move cash (INV-7), so it touches investments, the account's balance and statement,
 * and the transactions lists.
 */
function useInvalidateAfterMovement() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: financeKeys.investments() }),
      queryClient.invalidateQueries({ queryKey: financeKeys.accounts() }),
      queryClient.invalidateQueries({ queryKey: financeKeys.transactions() }),
    ])
}

function useInvalidateInvestments() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: financeKeys.investments() })
}

export function useCreateAssetClass() {
  const invalidate = useInvalidateInvestments()
  return useMutation({
    mutationFn: (body: { name: string; behavior: AssetBehavior }) => service.createAssetClass(body),
    onSuccess: invalidate,
  })
}

export function useRenameAssetClass() {
  const invalidate = useInvalidateInvestments()
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => service.renameAssetClass(id, name),
    onSuccess: invalidate,
  })
}

export function useSetAssetClassArchived() {
  const invalidate = useInvalidateInvestments()
  return useMutation({
    mutationFn: ({ id, archived }: { id: string; archived: boolean }) => service.setAssetClassArchived(id, archived),
    onSuccess: invalidate,
  })
}

export function useDeleteAssetClass() {
  const invalidate = useInvalidateInvestments()
  return useMutation({ mutationFn: (id: string) => service.deleteAssetClass(id), onSuccess: invalidate })
}

export function useCreateAsset() {
  const invalidate = useInvalidateInvestments()
  return useMutation({ mutationFn: (body: CreateAssetRequest) => service.createAsset(body), onSuccess: invalidate })
}

export function useUpdateAsset() {
  const invalidate = useInvalidateInvestments()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: Parameters<typeof service.updateAsset>[1] }) => service.updateAsset(id, body),
    onSuccess: invalidate,
  })
}

export function useSetAssetArchived() {
  const invalidate = useInvalidateInvestments()
  return useMutation({
    mutationFn: ({ id, archived }: { id: string; archived: boolean }) => service.setAssetArchived(id, archived),
    onSuccess: invalidate,
  })
}

export function useCreatePosition() {
  const invalidate = useInvalidateAfterMovement()
  return useMutation({ mutationFn: (body: CreatePositionRequest) => service.createPosition(body), onSuccess: invalidate })
}

export function useAddPositionMovement() {
  const invalidate = useInvalidateAfterMovement()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: AddPositionMovementRequest }) => service.addPositionMovement(id, body),
    onSuccess: invalidate,
  })
}

export function useUndoPositionMovement() {
  const invalidate = useInvalidateAfterMovement()
  return useMutation({ mutationFn: (id: string) => service.undoPositionMovement(id), onSuccess: invalidate })
}

export function useReportPositionValues() {
  const invalidate = useInvalidateAfterMovement()
  return useMutation({
    mutationFn: ({ valuedOn, items }: { valuedOn: string; items: { positionId: string; value: number }[] }) =>
      service.reportPositionValues(valuedOn, items),
    onSuccess: invalidate,
  })
}

export function useReportPositionValue() {
  const invalidate = useInvalidateAfterMovement()
  return useMutation({
    mutationFn: ({ id, valuedOn, value }: { id: string; valuedOn: string; value: number }) =>
      service.reportPositionValue(id, valuedOn, value),
    onSuccess: invalidate,
  })
}
