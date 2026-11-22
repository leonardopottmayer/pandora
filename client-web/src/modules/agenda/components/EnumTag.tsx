import { Tag } from 'antd'
import { useTranslation } from 'react-i18next'
import type { EnumMeta } from '../lib/enums'

/** Renders an antd Tag from an enum metadata entry (colour + i18n key). */
export function EnumTag({ meta }: { meta: EnumMeta | undefined }) {
  const { t } = useTranslation()
  if (!meta) return null
  return <Tag color={meta.color}>{t(meta.labelKey)}</Tag>
}
