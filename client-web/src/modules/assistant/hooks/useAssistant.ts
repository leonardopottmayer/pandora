import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { assistantKeys } from './queryKeys'
import * as assistantService from '../services/assistant.service'
import type { AssistantProfile } from '../models'

export function useAssistantProfile() {
  return useQuery({
    queryKey: assistantKeys.profile(),
    queryFn: () => assistantService.getProfile(),
  })
}

export function useAssistantProviders() {
  return useQuery({
    queryKey: assistantKeys.providers(),
    queryFn: () => assistantService.listProviders(),
  })
}

export function useSaveAssistantProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (profile: AssistantProfile) => assistantService.saveProfile(profile),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: assistantKeys.all })
    },
  })
}

export function useTestProvider() {
  return useMutation({
    mutationFn: ({ provider, model }: { provider: string; model?: string }) =>
      assistantService.testProvider(provider, model),
  })
}

export function useInvocations(limit = 50) {
  return useQuery({
    queryKey: assistantKeys.invocations(),
    queryFn: () => assistantService.listInvocations(limit),
  })
}

export function useInterpret() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ text, conversationId }: { text: string; conversationId?: string }) =>
      assistantService.interpret(text, conversationId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: assistantKeys.invocations() })
    },
  })
}

export function useConfirmInvocation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => assistantService.confirmInvocation(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: assistantKeys.invocations() })
    },
  })
}

export function useCancelInvocation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => assistantService.cancelInvocation(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: assistantKeys.invocations() })
    },
  })
}
