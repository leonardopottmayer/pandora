import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Select } from 'antd'
import type { SelectProps } from 'antd'
import type { UserCategoryDto } from '../../models'
import { useAccounts } from '../../hooks/useAccounts'
import { useCards, useCardStatements } from '../../hooks/useCards'
import { useUserCategories } from '../../hooks/useCategories'
import { useTags } from '../../hooks/useTags'
import { useTransactions } from '../../hooks/useTransactions'
import { useRecurringTransactions } from '../../hooks/useRecurringTransactions'
import { usePendingTransactions } from '../../hooks/usePendingTransactions'
import { formatDate, formatMoney, formatReferenceMonth } from '../../lib/format'

interface EntityIdSelectProps {
  entityType?: string
  value: string
  onChange: (value: string) => void
}
type SelectionProps = Omit<EntityIdSelectProps, 'entityType'>

function EntitySelect({ value, onChange, ...props }: SelectionProps & Pick<SelectProps, 'options' | 'loading' | 'disabled'> & { placeholder?: string }) {
  const { t } = useTranslation()
  return <Select className="w-full" showSearch={{ optionFilterProp: 'label' }} allowClear
    aria-label={props.placeholder ?? t('finances.audit.entityId')}
    placeholder={t('finances.audit.entityIdPlaceholder')} value={value || undefined}
    onChange={(id?: string) => onChange(id ?? '')} {...props} />
}

function AccountSelect(props: SelectionProps) {
  const { data, isLoading } = useAccounts()
  return <EntitySelect {...props} loading={isLoading} options={(data ?? []).map((a) => ({ value: a.id, label: `${a.name} (${a.currency})` }))} />
}
function CardSelect(props: SelectionProps) {
  const { data, isLoading } = useCards()
  return <EntitySelect {...props} loading={isLoading} options={(data ?? []).map((card) => ({ value: card.id, label: card.lastFour ? `${card.name} ····${card.lastFour}` : card.name }))} />
}
function TagSelect(props: SelectionProps) {
  const { data, isLoading } = useTags()
  return <EntitySelect {...props} loading={isLoading} options={(data ?? []).map((tag) => ({ value: tag.id, label: tag.name }))} />
}
function categoryOptions(categories: UserCategoryDto[]): { value: string; label: string }[] {
  return categories.flatMap((category) => [{ value: category.id, label: category.name }, ...categoryOptions(category.children)])
}
function CategorySelect(props: SelectionProps) {
  const { data, isLoading } = useUserCategories()
  return <EntitySelect {...props} loading={isLoading} options={categoryOptions(data ?? [])} />
}
function TransactionSelect(props: SelectionProps) {
  const { data, isLoading } = useTransactions({ take: 100 })
  return <EntitySelect {...props} loading={isLoading} options={(data ?? []).map((tx) => ({ value: tx.id, label: `${formatDate(tx.occurredOn)} · ${tx.description} · ${formatMoney(tx.amount, tx.currency)}` }))} />
}
function RecurringSelect(props: SelectionProps) {
  const { data, isLoading } = useRecurringTransactions()
  return <EntitySelect {...props} loading={isLoading} options={(data ?? []).map((r) => ({ value: r.id, label: r.name }))} />
}
function PendingSelect(props: SelectionProps) {
  const { data, isLoading } = usePendingTransactions({ take: 100 })
  return <EntitySelect {...props} loading={isLoading} options={(data ?? []).map((p) => ({ value: p.id, label: `${formatDate(p.occurredOn)} · ${p.description}` }))} />
}
function StatementSelect(props: SelectionProps) {
  const { t } = useTranslation()
  const [cardId, setCardId] = useState('')
  const { data: cards, isLoading: loadingCards } = useCards()
  const { data: statements, isLoading: loadingStatements } = useCardStatements(cardId)
  return <div className="flex min-w-0 flex-col gap-2 sm:flex-row">
    <EntitySelect value={cardId} onChange={(id) => { setCardId(id); props.onChange('') }} loading={loadingCards}
      placeholder={t('finances.transactions.selectCard')} options={(cards ?? []).map((card) => ({ value: card.id, label: card.lastFour ? `${card.name} ····${card.lastFour}` : card.name }))} />
    <EntitySelect {...props} loading={loadingStatements} disabled={!cardId} placeholder={t('finances.audit.selectStatement')}
      options={(statements ?? []).map((s) => ({ value: s.id, label: formatReferenceMonth(s.referenceMonth) }))} />
  </div>
}

/** Mount only the selector for the chosen entity type, so unrelated lists are not fetched. */
export function EntityIdSelect({ entityType, ...props }: EntityIdSelectProps) {
  switch (entityType) {
    case 'account': return <AccountSelect {...props} />
    case 'card': return <CardSelect {...props} />
    case 'tag': return <TagSelect {...props} />
    case 'user-category': return <CategorySelect {...props} />
    case 'transaction': return <TransactionSelect {...props} />
    case 'recurring-transaction': return <RecurringSelect {...props} />
    case 'pending-transaction': return <PendingSelect {...props} />
    case 'statement': return <StatementSelect {...props} />
    default: return null
  }
}
