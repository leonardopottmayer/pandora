import { apiClient } from '@/lib/api/client'
import type { MoveTreeItemsRequest } from '../models'

export async function moveTreeItems(body: MoveTreeItemsRequest): Promise<void> {
  await apiClient.post('/api/v1/notes/tree/move', body)
}
