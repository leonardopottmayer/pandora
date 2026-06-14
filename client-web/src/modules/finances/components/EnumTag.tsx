import { Tag } from 'antd'
import { useTranslation } from 'react-i18next'
import type { EnumMeta } from '../lib/enums'

/** Renderiza um antd Tag a partir do metadado de enum (cor + chave i18n). */
export function EnumTag({ meta }: { meta: EnumMeta | undefined }) {
  const { t } = useTranslation()
  if (!meta) return null
  return <Tag color={meta.color}>{t(meta.labelKey)}</Tag>
}
