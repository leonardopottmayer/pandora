import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import type { ImportFileFilters } from '../models'
import * as importsService from '../services/imports.service'

export function useImportFiles(filters: ImportFileFilters = {}) {
  return useQuery({
    queryKey: financeKeys.importList(filters),
    queryFn: () => importsService.listImportFiles(filters),
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

export function useImportRows(importFileId: string) {
  return useQuery({
    queryKey: financeKeys.importRows(importFileId),
    queryFn: () => importsService.getImportRows(importFileId),
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
