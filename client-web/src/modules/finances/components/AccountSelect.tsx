import { useTranslation } from 'react-i18next'
import { Select } from 'antd'
import { useAccounts } from '../hooks/useAccounts'

interface AccountSelectProps {
  value?: string
  onChange?: (value?: string) => void
  disabled?: boolean
  /** Exibe um botão para limpar a seleção. */
  allowClear?: boolean
  /** IDs a excluir das opções (ex.: a conta de origem numa transferência). */
  excludeIds?: string[]
}

/** Seleciona uma conta ativa do usuário. */
export function AccountSelect({ value, onChange, disabled, allowClear, excludeIds = [] }: AccountSelectProps) {
  const { t } = useTranslation()
  const { data: accounts, isLoading } = useAccounts()

  return (
    <Select
      style={{ width: '100%' }}
      disabled={disabled}
      allowClear={allowClear}
      loading={isLoading}
      showSearch
      optionFilterProp="label"
      placeholder={t('finances.transactions.selectAccount')}
      value={value}
      onChange={onChange}
      options={(accounts ?? [])
        .filter((a) => !excludeIds.includes(a.id))
        .map((a) => ({ value: a.id, label: `${a.name} (${a.currency})` }))}
    />
  )
}
