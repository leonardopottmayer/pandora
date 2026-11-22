import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { noteKeys } from './queryKeys'
import * as dailyNotes from '../services/dailyNotes.service'

export function useOpenDailyNote() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: dailyNotes.openDailyNote,
    onSuccess: () => {
      // A visit can create the space, folder, page and its derived data. Leave open drafts alone.
      client.invalidateQueries({ queryKey: [...noteKeys.pages(), 'tree'] })
      client.invalidateQueries({ queryKey: noteKeys.spaces() })
      client.invalidateQueries({ queryKey: noteKeys.folders() })
      client.invalidateQueries({ queryKey: noteKeys.tags() })
      client.invalidateQueries({ queryKey: noteKeys.allGraphs() })
      client.invalidateQueries({ queryKey: noteKeys.allBacklinks() })
    },
  })
}

export function useNotesPreferences() {
  return useQuery({ queryKey: noteKeys.preferences(), queryFn: dailyNotes.getNotesPreferences })
}

export function useSaveNotesPreferences() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: dailyNotes.saveNotesPreferences,
    onSuccess: (preferences) => client.setQueryData(noteKeys.preferences(), preferences),
  })
}
