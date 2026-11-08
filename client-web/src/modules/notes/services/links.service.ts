import { apiClient } from '@/lib/api/client'
import type { LinkPreviewDto } from '../models'

/** A web page's title, image and oEmbed, read by the server. */
export async function getLinkPreview(url: string): Promise<LinkPreviewDto> {
  const { data } = await apiClient.get<LinkPreviewDto>('/api/v1/notes/links/preview', { params: { url } })
  return data
}
