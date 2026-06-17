import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Select, Space } from 'antd'
import type { UserCategoryDto } from '../../models'
import { useAccounts } from '../../hooks/useAccounts'
import { useCards, useCardStatements } from '../../hooks/useCards'
import { useUserCategories } from '../../hooks/useCategories'
import { useTags } from '../../hooks/useTags'
import { useTransactions } from '../../hooks/useTransactions'
import { useRecurringTransactions } from '../../hooks/useRecurringTransactions'
import { usePendingTransactions } from '../../hooks/usePendingTransactions'
import { formatDate, formatMoney, formatReferenceMonth } from '../../lib/format'

function flattenUserCategoryOptions(categories: UserCategoryDto[]): { value: string; label: string }[] {
  const options: { value: string; label: string }[] = []
  for (const category of categories) {
    options.push({ value: category.id, label: category.name })
    if (category.children.length) options.push(...flattenUserCategoryOptions(category.children))
  }
  return options
}

interface EntityIdSelectProps {
  entityType?: string
  value: string
  onChange: (value: string) => void
}

/** Selects the entity ID from the user's entities based on the chosen type. */
export function EntityIdSelect({ entityType, value, onChange }: EntityIdSelectProps) {
  const { t } = useTranslation()
  const [statementCardId, setStatementCardId] = useState<string>()

  const { data: accounts, isLoading: loadingAccounts } = useAccounts()
  const { data: cards, isLoading: loadingCards } = useCards()
  const { data: tags, isLoading: loadingTags } = useTags()
  const { data: userCategories, isLoading: loadingUserCategories } = useUserCategories()
  const { data: transactions, isLoading: loadingTransactions } = useTransactions({ take: 100 })
  const { data: recurring, isLoading: loadingRecurring } = useRecurringTransactions()
  const { data: pending, isLoading: loadingPending } = usePendingTransactions({ take: 100 })
  const { data: statements, isLoading: loadingStatements } = useCardStatements(statementCardId ?? '')

  const commonProps = {
    style: { minWidth: 280 },
    showSearch: true,
    allowClear: true,
    optionFilterProp: 'label',
    placeholder: t('finances.audit.entityIdPlaceholder'),
    value: value || undefined,
    onChange: (v?: string) => onChange(v ?? ''),
  }

  switch (entityType) {
    case 'account':
      return (
        <Select
          {...commonProps}
          loading={loadingAccounts}
          options={(accounts ?? []).map((a) => ({ value: a.id, label: `${a.name} (${a.currency})` }))}
        />
      )

    case 'card':
      return (
        <Select
          {...commonProps}
          loading={loadingCards}
          options={(cards ?? []).map((c) => ({
            value: c.id,
            label: c.lastFour ? `${c.name} ····${c.lastFour}` : c.name,
          }))}
        />
      )

    case 'tag':
      return (
        <Select
          {...commonProps}
          loading={loadingTags}
          options={(tags ?? []).map((tag) => ({ value: tag.id, label: tag.name }))}
        />
      )

    case 'user-category':
      return (
        <Select
          {...commonProps}
          loading={loadingUserCategories}
          options={flattenUserCategoryOptions(userCategories ?? [])}
        />
      )

    case 'transaction':
      return (
        <Select
          {...commonProps}
          style={{ minWidth: 360 }}
          loading={loadingTransactions}
          options={(transactions ?? []).map((tx) => ({
            value: tx.id,
            label: `${formatDate(tx.occurredOn)} · ${tx.description} · ${formatMoney(tx.amount, tx.currency)}`,
          }))}
        />
      )

    case 'recurring-transaction':
      return (
        <Select
          {...commonProps}
          loading={loadingRecurring}
          options={(recurring ?? []).map((r) => ({ value: r.id, label: r.name }))}
        />
      )

    case 'pending-transaction':
      return (
        <Select
          {...commonProps}
          style={{ minWidth: 360 }}
          loading={loadingPending}
          options={(pending ?? []).map((p) => ({
            value: p.id,
            label: `${formatDate(p.occurredOn)} · ${p.description}`,
          }))}
        />
      )

    case 'statement':
      return (
        <Space>
          <Select
            style={{ minWidth: 200 }}
            showSearch
            allowClear
            optionFilterProp="label"
            loading={loadingCards}
            placeholder={t('finances.transactions.selectCard')}
            value={statementCardId}
            onChange={(v?: string) => {
              setStatementCardId(v)
              onChange('')
            }}
            options={(cards ?? []).map((c) => ({
              value: c.id,
              label: c.lastFour ? `${c.name} ····${c.lastFour}` : c.name,
            }))}
          />
          <Select
            {...commonProps}
            style={{ minWidth: 200 }}
            loading={loadingStatements}
            disabled={!statementCardId}
            placeholder={t('finances.audit.selectStatement')}
            options={(statements ?? []).map((s) => ({ value: s.id, label: formatReferenceMonth(s.referenceMonth) }))}
          />
        </Space>
      )

    default:
      return null
  }
}
