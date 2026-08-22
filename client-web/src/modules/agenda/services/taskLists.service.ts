import { apiClient } from '@/lib/api/client'
import type { CreateTaskListRequest, TaskListDto, UpdateTaskListRequest } from '../models'

const BASE = '/api/v1.0/agenda/task-lists'

export async function listTaskLists(): Promise<TaskListDto[]> {
  const { data } = await apiClient.get<TaskListDto[]>(BASE)
  return data
}

export async function createTaskList(body: CreateTaskListRequest): Promise<TaskListDto> {
  const { data } = await apiClient.post<TaskListDto>(BASE, body)
  return data
}

export async function updateTaskList(
  id: string,
  body: UpdateTaskListRequest,
): Promise<TaskListDto> {
  const { data } = await apiClient.patch<TaskListDto>(`${BASE}/${id}`, body)
  return data
}

export async function deleteTaskList(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`)
}
