import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { agendaKeys } from './queryKeys'
import * as preferencesService from '../services/preferences.service'

export function useAgendaPreferences() {
  return useQuery({ queryKey: agendaKeys.preferences(), queryFn: preferencesService.getPreferences })
}

export function useSaveAgendaPreferences() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: preferencesService.savePreferences,
    onSuccess: (saved) => queryClient.setQueryData(agendaKeys.preferences(), saved),
  })
}
