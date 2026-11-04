import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import type { ImportFileFilters, ImportStatus } from '../models'
import * as importsService from '../services/imports.service'

export function useImportFiles(filters: ImportFileFilters = {}) {
  return useQuery({
    queryKey: financeKeys.importList(filters),
    queryFn: () => importsService.listImportFiles(filters),
  })
}

/** Complete history, refreshed while any file is waiting or being parsed. */
export function useAllImportFiles() {
  return useQuery({
    queryKey: financeKeys.importList({ take: -1 }),
    queryFn: importsService.listAllImportFiles,
    refetchInterval: (query) => query.state.data?.some((file) => file.status === 'received' || file.status === 'parsing') ? 5000 : false,
  })
}

export function useImportFile(id: string) {
  return useQuery({
    queryKey: financeKeys.import(id),
    queryFn: () => importsService.getImportFile(id),
    refetchInterval: (q) => {
      const status = q.state.data?.status
      // Poll while the file is in a non-terminal state so the UI updates when parsing finishes
      return status === 'received' || status === 'parsing' ? 5000 : false
    },
  })
}

export function useImportRows(importFileId: string, phase?: ImportStatus) {
  return useQuery({
    // A new processing phase fetches its final rows even when the polling interval stops.
    queryKey: financeKeys.importRows(importFileId, phase),
    queryFn: () => importsService.getImportRows(importFileId),
    enabled: !!importFileId,
    refetchInterval: phase === 'received' || phase === 'parsing' ? 5000 : false,
  })
}

export function useImportLayouts() {
  return useQuery({
    queryKey: financeKeys.importLayouts(),
    queryFn: () => importsService.listImportLayouts(),
    staleTime: Infinity,
  })
}

export function useUploadImportFile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      file,
      accountId,
      cardId,
      cutoffDate,
    }: {
      file: File
      accountId?: string
      cardId?: string
      cutoffDate?: string
    }) => importsService.uploadImportFile(file, accountId, cardId, cutoffDate),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: financeKeys.imports() })
    },
  })
}

export function useAbortImportFile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => importsService.abortImportFile(id),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: financeKeys.imports() })
      queryClient.invalidateQueries({ queryKey: financeKeys.import(id) })
    },
  })
}

export function useRetryImportFile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => importsService.retryImportFile(id),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: financeKeys.imports() })
      queryClient.invalidateQueries({ queryKey: financeKeys.import(id) })
    },
  })
}
