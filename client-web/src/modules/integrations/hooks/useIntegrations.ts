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

export function useIntegrationEvents(limit = 50) {
  return useQuery({
    queryKey: integrationsKeys.events(limit),
    queryFn: () => integrationsService.listEvents(limit),
  })
}

export function useStartConnection() {
  return useMutation({
    mutationFn: ({ provider, redirectAfter }: { provider: string; redirectAfter: string }) =>
      integrationsService.startConnection(provider, redirectAfter),
  })
}

export function useSaveApiKey() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ provider, apiKey }: { provider: string; apiKey: string }) =>
      integrationsService.saveApiKey(provider, apiKey),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: integrationsKeys.all })
    },
  })
}

export function useDisconnectAccount() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => integrationsService.disconnectAccount(id),
    onSuccess: () => {
      // The prefix covers providers, accounts and the event log in one shot.
      queryClient.invalidateQueries({ queryKey: integrationsKeys.all })
    },
  })
}
