export type ChannelId = 'email' | 'telegram'

/** All channels the settings screen can present, in display order. */
export const ALL_CHANNELS: ChannelId[] = ['email', 'telegram']

/** Categories the user can configure. Security categories are mandatory and not shown. */
export const NOTIFICATION_CATEGORIES = ['agenda.reminder', 'agenda.task', 'agenda.event'] as const
export type NotificationCategory = (typeof NOTIFICATION_CATEGORIES)[number]

export interface UserChannel {
  channel: ChannelId
  address: string
  isVerified: boolean
  isEnabled: boolean
  disabledReason: string | null
  verifiedAt: string | null
}

export interface ChannelLink {
  /** Deep link the user taps to finish linking. */
  url: string
  expiresAt: string
}

export interface NotificationPreference {
  category: string
  channels: ChannelId[]
}
