import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { integrationsKeys } from './queryKeys'
import * as integrationsService from '../services/integrations.service'

export function useProviders() {
  return useQuery({
    queryKey: integrationsKeys.providers(),
    queryFn: () => integrationsService.listProviders(),
  })
}

export function useAccounts() {
  return useQuery({
    queryKey: integrationsKeys.accounts(),
    queryFn: () => integrationsService.listAccounts(),
  })
}

export function useStartConnection() {
  return useMutation({
    mutationFn: ({ provider, redirectAfter }: { provider: string; redirectAfter: string }) =>
      integrationsService.startConnection(provider, redirectAfter),
  })
}

export function useDisconnectAccount() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => integrationsService.disconnectAccount(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: integrationsKeys.accounts() })
      queryClient.invalidateQueries({ queryKey: integrationsKeys.providers() })
    },
  })
}
