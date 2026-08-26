import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { channelsKeys } from './queryKeys'
import type { ChannelId, DeliveryHistoryFilters } from '../models'
import * as channelsService from '../services/channels.service'

export function useChannels() {
  return useQuery({
    queryKey: channelsKeys.channelList(),
    queryFn: () => channelsService.listChannels(),
  })
}

export function useNotificationPreferences() {
  return useQuery({
    queryKey: channelsKeys.preferences(),
    queryFn: () => channelsService.listPreferences(),
  })
}

export function useDeliveryHistory(filters: DeliveryHistoryFilters) {
  return useQuery({
    queryKey: channelsKeys.history(filters),
    queryFn: () => channelsService.listDeliveryHistory(filters),
  })
}

function useInvalidateChannels() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: channelsKeys.channelList() })
}

export function useLinkChannel() {
  return useMutation({
    mutationFn: (channel: ChannelId) => channelsService.linkChannel(channel),
  })
}

export function useUnlinkChannel() {
  const invalidate = useInvalidateChannels()
  return useMutation({
    mutationFn: (channel: ChannelId) => channelsService.unlinkChannel(channel),
    onSuccess: invalidate,
  })
}

export function useTestChannel() {
  return useMutation({
    mutationFn: (channel: ChannelId) => channelsService.testChannel(channel),
  })
}

export function useSetPreference() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ category, channels }: { category: string; channels: ChannelId[] }) =>
      channelsService.setPreference(category, channels),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: channelsKeys.preferences() }),
  })
}
