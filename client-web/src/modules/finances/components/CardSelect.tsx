import { useTranslation } from 'react-i18next'
import { Select } from 'antd'
import { useCards } from '../hooks/useCards'

interface CardSelectProps {
  value?: string
  onChange?: (value: string) => void
  disabled?: boolean
}

/** Seleciona um cartão ativo do usuário. */
export function CardSelect({ value, onChange, disabled }: CardSelectProps) {
  const { t } = useTranslation()
  const { data: cards, isLoading } = useCards()

  return (
    <Select
      style={{ width: '100%' }}
      disabled={disabled}
      loading={isLoading}
      showSearch
      optionFilterProp="label"
      placeholder={t('finances.transactions.selectCard')}
      value={value}
      onChange={onChange}
      options={(cards ?? []).map((c) => ({
        value: c.id,
        label: c.lastFour ? `${c.name} ····${c.lastFour}` : c.name,
      }))}
    />
  )
}
