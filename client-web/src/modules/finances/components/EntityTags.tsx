import { useTranslation } from 'react-i18next'
import { App } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import type { TaggableEntityType } from '../models'
import { useEntityTags, useSetEntityTags } from '../hooks/useTags'
import { TagPicker } from './TagPicker'

/** The tags of one entity, saved as soon as the selection changes. */
export function EntityTags({ entityType, entityId }: { entityType: TaggableEntityType; entityId: string }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { data, isLoading } = useEntityTags(entityType, entityId)
  const save = useSetEntityTags(entityType, entityId)

  function change(tagIds: string[]) {
    save.mutate(tagIds, { onError: (err) => message.error(toErrorMessage(err, t('finances.tags.linkError'))) })
  }

  return (
    <TagPicker
      aria-label={t('finances.tags.label')}
      value={save.isPending ? save.variables : data}
      onChange={change}
      disabled={isLoading}
    />
  )
}
