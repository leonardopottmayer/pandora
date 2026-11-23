import { useTranslation } from 'react-i18next'
import { Select } from 'antd'
import type { CardDto } from '../models'
import { cardNumberLabel, selectableCardNumbers } from '../lib/cardNumbers'

interface CardNumberSelectProps {
  card: CardDto | undefined
  value?: string | null
  onChange?: (value: string | null) => void
}

/** Optional choice of which of the card's numbers (physical or virtual) was used. */
export function CardNumberSelect({ card, value, onChange }: CardNumberSelectProps) {
  const { t } = useTranslation()

  return (
    <Select
      style={{ width: '100%' }}
      allowClear
      placeholder={t('finances.transactions.cardNumberPlaceholder')}
      value={value ?? undefined}
      onChange={(next) => onChange?.(next ?? null)}
      options={selectableCardNumbers(card, value).map((number) => ({ value: number.id, label: cardNumberLabel(number, t) }))}
    />
  )
}
