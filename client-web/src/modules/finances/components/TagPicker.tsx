import { useTranslation } from 'react-i18next'
import { Select } from 'antd'
import { useTags } from '../hooks/useTags'

interface TagPickerProps {
  value?: string[]
  onChange?: (value: string[]) => void
  disabled?: boolean
  placeholder?: string
  className?: string
  'aria-label'?: string
}

/** Multi-select tag picker for the user (returns the list of tagIds). Full width unless given a className. */
export function TagPicker({ value, onChange, disabled, placeholder, className, 'aria-label': ariaLabel }: TagPickerProps) {
  const { t } = useTranslation()
  const { data: tags, isLoading } = useTags()

  return (
    <Select
      mode="multiple"
      className={className}
      style={className ? undefined : { width: '100%' }}
      aria-label={ariaLabel}
      disabled={disabled}
      loading={isLoading}
      allowClear
      placeholder={placeholder ?? t('finances.tags.selectPlaceholder')}
      value={value}
      onChange={onChange}
      optionFilterProp="label"
      options={(tags ?? []).map((tag) => ({ value: tag.id, label: tag.name }))}
    />
  )
}
