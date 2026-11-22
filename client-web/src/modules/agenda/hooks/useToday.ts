import { useQuery } from '@tanstack/react-query'
import { agendaKeys } from './queryKeys'
import * as todayService from '../services/today.service'

export function useToday() {
  return useQuery({
    queryKey: agendaKeys.today(),
    queryFn: () => todayService.getToday(),
  })
}
