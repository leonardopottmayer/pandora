import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { integrationsKeys } from './queryKeys'
import * as integrationsService from '../services/integrations.service'
import type { StartConnectionOptions } from '../services/integrations.service'

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
    mutationFn: ({
      provider,
      redirectAfter,
      ...options
    }: { provider: string; redirectAfter: string } & StartConnectionOptions) =>
      integrationsService.startConnection(provider, redirectAfter, options),
  })
}

/** Any write to accounts: the prefix covers providers, accounts and the event log in one shot. */
function useAccountMutation<TArgs>(mutationFn: (args: TArgs) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: integrationsKeys.all })
    },
  })
}

export function useSaveApiKey() {
  return useAccountMutation(({ provider, apiKey, label }: { provider: string; apiKey: string; label?: string }) =>
    integrationsService.saveApiKey(provider, apiKey, label),
  )
}

export function useSavePassword() {
  return useAccountMutation(
    ({ provider, username, password, label }: { provider: string; username: string; password: string; label?: string }) =>
      integrationsService.savePassword(provider, username, password, label),
  )
}

export function useReplaceSecret() {
  return useAccountMutation(({ id, secret }: { id: string; secret: string }) =>
    integrationsService.replaceSecret(id, secret),
  )
}

export function useRenameAccount() {
  return useAccountMutation(({ id, label }: { id: string; label: string }) =>
    integrationsService.renameAccount(id, label),
  )
}

export function useDisconnectAccount() {
  return useAccountMutation((id: string) => integrationsService.disconnectAccount(id))
}
