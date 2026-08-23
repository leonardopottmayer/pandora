import { apiClient } from '@/lib/api/client'
import type { ChannelId, ChannelLink, NotificationPreference, UserChannel } from '../models'

const BASE = '/api/v1.0/channels'

export async function listChannels(): Promise<UserChannel[]> {
  const { data } = await apiClient.get<UserChannel[]>(BASE)
  return data
}

export async function linkChannel(channel: ChannelId): Promise<ChannelLink> {
  const { data } = await apiClient.post<ChannelLink>(`${BASE}/${channel}/link`)
  return data
}

export async function unlinkChannel(channel: ChannelId): Promise<void> {
  await apiClient.delete(`${BASE}/${channel}/link`)
}

export async function testChannel(channel: ChannelId): Promise<void> {
  await apiClient.post(`${BASE}/${channel}/test`)
}

export async function listPreferences(): Promise<NotificationPreference[]> {
  const { data } = await apiClient.get<NotificationPreference[]>(`${BASE}/preferences`)
  return data
}

export async function setPreference(category: string, channels: ChannelId[]): Promise<void> {
  await apiClient.put(`${BASE}/preferences/${category}`, { channels })
}
