import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { agendaKeys } from './queryKeys'
import type { CreateTaskListRequest, UpdateTaskListRequest } from '../models'
import * as taskListsService from '../services/taskLists.service'

export function useTaskLists() {
  return useQuery({
    queryKey: agendaKeys.taskListList(),
    queryFn: () => taskListsService.listTaskLists(),
  })
}

function useInvalidateTaskLists() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: agendaKeys.taskLists() })
}

export function useCreateTaskList() {
  const invalidate = useInvalidateTaskLists()
  return useMutation({
    mutationFn: (body: CreateTaskListRequest) => taskListsService.createTaskList(body),
    onSuccess: invalidate,
  })
}

export function useUpdateTaskList() {
  const invalidate = useInvalidateTaskLists()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateTaskListRequest }) =>
      taskListsService.updateTaskList(id, body),
    onSuccess: invalidate,
  })
}

export function useDeleteTaskList() {
  const invalidate = useInvalidateTaskLists()
  return useMutation({
    mutationFn: (id: string) => taskListsService.deleteTaskList(id),
    onSuccess: invalidate,
  })
}
