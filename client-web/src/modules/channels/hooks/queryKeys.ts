import type { DeliveryHistoryFilters } from '../models'

// Central query key factory for the channels module.
export const channelsKeys = {
  all: ['channels'] as const,
  channelList: () => [...channelsKeys.all, 'list'] as const,
  preferences: () => [...channelsKeys.all, 'preferences'] as const,
  settings: () => [...channelsKeys.all, 'settings'] as const,
  history: (filters: DeliveryHistoryFilters) => [...channelsKeys.all, 'history', filters] as const,
}
