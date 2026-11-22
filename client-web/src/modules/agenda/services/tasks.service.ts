import { apiClient } from '@/lib/api/client'
import type { CreateTaskRequest, TaskDto, TaskFilters, UpdateTaskRequest } from '../models'

const BASE = '/api/v1.0/agenda/tasks'

export async function listTasks(filters: TaskFilters = {}): Promise<TaskDto[]> {
  const { data } = await apiClient.get<TaskDto[]>(BASE, {
    params: { listId: filters.listId, status: filters.status, due: filters.due },
  })
  return data
}

export async function createTask(body: CreateTaskRequest): Promise<TaskDto> {
  const { data } = await apiClient.post<TaskDto>(BASE, body)
  return data
}

export async function updateTask(id: string, body: UpdateTaskRequest): Promise<TaskDto> {
  const { data } = await apiClient.patch<TaskDto>(`${BASE}/${id}`, body)
  return data
}

export async function completeTask(id: string): Promise<TaskDto> {
  const { data } = await apiClient.post<TaskDto>(`${BASE}/${id}/complete`)
  return data
}

export async function reopenTask(id: string): Promise<TaskDto> {
  const { data } = await apiClient.post<TaskDto>(`${BASE}/${id}/reopen`)
  return data
}

export async function deleteTask(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`)
}
