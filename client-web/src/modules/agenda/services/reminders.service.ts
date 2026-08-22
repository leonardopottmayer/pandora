import { apiClient } from '@/lib/api/client'
import type { CreateReminderRequest, ReminderDto, SnoozeReminderRequest } from '../models'

const BASE = '/api/v1.0/agenda/reminders'

export async function listReminders(): Promise<ReminderDto[]> {
  const { data } = await apiClient.get<ReminderDto[]>(BASE)
  return data
}

export async function createReminder(body: CreateReminderRequest): Promise<ReminderDto> {
  const { data } = await apiClient.post<ReminderDto>(BASE, body)
  return data
}

export async function acknowledgeReminder(id: string): Promise<ReminderDto> {
  const { data } = await apiClient.post<ReminderDto>(`${BASE}/${id}/acknowledge`)
  return data
}

export async function snoozeReminder(
  id: string,
  body: SnoozeReminderRequest,
): Promise<ReminderDto> {
  const { data } = await apiClient.post<ReminderDto>(`${BASE}/${id}/snooze`, body)
  return data
}

export async function deleteReminder(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/${id}`)
}
