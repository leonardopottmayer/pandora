import { listReminders } from '@/modules/agenda/services/reminders.service'
import { listTasks } from '@/modules/agenda/services/tasks.service'
import { listAccounts } from '@/modules/finances/services/accounts.service'
import { listCards } from '@/modules/finances/services/cards.service'
import type { ItemChip } from '../lib/blocks'

// Items of other modules a page can name after `@`: open tasks and scheduled reminders (Agenda),
// accounts and cards (Finances). A module the user cannot read just offers nothing.

async function settle<T>(load: Promise<T[]>, map: (item: T) => ItemChip | null): Promise<ItemChip[]> {
  try {
    return (await load).map(map).filter((item): item is ItemChip => item !== null)
  } catch {
    return []
  }
}

export async function listModuleItems(): Promise<ItemChip[]> {
  const groups = await Promise.all([
    settle(listTasks(), (task) => (task.status === 'Done' || task.status === 'Cancelled' ? null : { module: 'agenda', kind: 'task', id: task.id, label: task.title })),
    settle(listReminders(), (reminder) => (reminder.status === 'Scheduled' || reminder.status === 'Snoozed' ? { module: 'agenda', kind: 'reminder', id: reminder.id, label: reminder.title } : null)),
    settle(listAccounts(), (account) => ({ module: 'finances', kind: 'account', id: account.id, label: account.name })),
    settle(listCards(), (card) => ({ module: 'finances', kind: 'card', id: card.id, label: card.name })),
  ])
  return groups.flat()
}

/** Where an item opens: its own page when the module has one, else the module's list. */
export function itemRoute(item: ItemChip): string | null {
  if (item.module === 'agenda') return item.kind === 'reminder' ? '/agenda/reminders' : '/agenda/tasks'
  if (item.module === 'finances') return item.kind === 'card' ? `/finances/cards/${item.id}` : `/finances/accounts/${item.id}`
  return null
}

export const ITEM_ICONS: Record<string, string> = { task: '☑️', reminder: '⏰', account: '🏦', card: '💳' }
