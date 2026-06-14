import { useTranslation } from 'react-i18next'
import { Select } from 'antd'
import { useTags } from '../hooks/useTags'

interface TagPickerProps {
  value?: string[]
  onChange?: (value: string[]) => void
  disabled?: boolean
}

/** Multi-seleção de tags do usuário (retorna a lista de tagIds). */
export function TagPicker({ value, onChange, disabled }: TagPickerProps) {
  const { t } = useTranslation()
  const { data: tags, isLoading } = useTags()

  return (
    <Select
      mode="multiple"
      style={{ width: '100%' }}
      disabled={disabled}
      loading={isLoading}
      allowClear
      placeholder={t('finances.tags.selectPlaceholder')}
      value={value}
      onChange={onChange}
      optionFilterProp="label"
      options={(tags ?? []).map((tag) => ({ value: tag.id, label: tag.name }))}
    />
  )
}
