import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { agendaKeys } from './queryKeys'
import type { CreateTaskRequest, TaskFilters, UpdateTaskRequest } from '../models'
import * as tasksService from '../services/tasks.service'

export function useTasks(filters: TaskFilters = {}) {
  return useQuery({
    queryKey: agendaKeys.taskList(filters),
    queryFn: () => tasksService.listTasks(filters),
  })
}

/** Invalidates tasks and the Today view (which includes tasks). */
function useInvalidateTasks() {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: agendaKeys.tasks() })
    queryClient.invalidateQueries({ queryKey: agendaKeys.today() })
  }
}

export function useCreateTask() {
  const invalidate = useInvalidateTasks()
  return useMutation({
    mutationFn: (body: CreateTaskRequest) => tasksService.createTask(body),
    onSuccess: invalidate,
  })
}

export function useUpdateTask() {
  const invalidate = useInvalidateTasks()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateTaskRequest }) =>
      tasksService.updateTask(id, body),
    onSuccess: invalidate,
  })
}

export function useCompleteTask() {
  const invalidate = useInvalidateTasks()
  return useMutation({
    mutationFn: (id: string) => tasksService.completeTask(id),
    onSuccess: invalidate,
  })
}

export function useReopenTask() {
  const invalidate = useInvalidateTasks()
  return useMutation({
    mutationFn: (id: string) => tasksService.reopenTask(id),
    onSuccess: invalidate,
  })
}

export function useDeleteTask() {
  const invalidate = useInvalidateTasks()
  return useMutation({
    mutationFn: (id: string) => tasksService.deleteTask(id),
    onSuccess: invalidate,
  })
}
