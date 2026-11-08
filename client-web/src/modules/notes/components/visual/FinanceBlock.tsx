import { useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQueryClient, type QueryKey } from '@tanstack/react-query'
import { Alert, Button, Select, Spin, Typography } from 'antd'
import { ReloadOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import { useFeature, usePermission } from '@/modules/identity/hooks/useAccess'
import { useAccounts, useAccountBalance } from '@/modules/finances/hooks/useAccounts'
import { useCards, useCardAvailableLimit } from '@/modules/finances/hooks/useCards'
import { financeKeys } from '@/modules/finances/hooks/queryKeys'
import { formatMoney } from '@/modules/finances/lib/format'
import { StatTile } from '@/modules/finances/components/StatTile'
import { useVisualEditorContext } from './visualContext'
import { FinanceExpenses } from './FinanceExpenses'

interface FinanceBlockProps { props: Record<string, unknown>; editable: boolean; update: (props: Record<string, unknown>) => void }
type Mode = 'account' | 'card' | 'expenses'

/** Reads current financial figures without copying values, names or balances into the note. */
export function FinanceBlock({ props, editable, update }: FinanceBlockProps) {
  const { t } = useTranslation()
  const { publicView } = useVisualEditorContext()
  const client = useQueryClient()
  const enabled = useFeature('finances')
  const accounts = usePermission('finances.accounts.read')
  const cards = usePermission('finances.cards.read')
  const transactions = usePermission('finances.transactions.read')
  const categories = usePermission('finances.categories.read')
  let mode: Mode = 'account'
  if (props.mode === 'card') mode = 'card'
  else if (props.mode === 'expenses') mode = 'expenses'
  const access = { account: accounts, card: cards, expenses: transactions && categories }
  const [refreshing, setRefreshing] = useState(false)

  // Even a populated private cache must not be mounted/read through a public note.
  if (publicView) return <div contentEditable={false}><strong>{t('notes.financeBlock.title')}</strong><p>{t('notes.agendaBlock.private')}</p></div>
  if (!enabled) return <p contentEditable={false}>{t('notes.financeBlock.unavailable')}</p>

  async function refresh() {
    let keys: QueryKey[] = [financeKeys.accounts()]
    if (mode === 'card') keys = [financeKeys.cards()]
    else if (mode === 'expenses') keys = [financeKeys.transactions(), financeKeys.categories()]
    setRefreshing(true)
    try { await Promise.all(keys.map((queryKey) => client.invalidateQueries({ queryKey }))) }
    finally { setRefreshing(false) }
  }

  let body: ReactNode
  if (!access[mode]) body = <Typography.Text type="secondary">{t('notes.financeBlock.unavailable')}</Typography.Text>
  else if (mode === 'account') body = <AccountSource id={typeof props.accountId === 'string' ? props.accountId : null} editable={editable} onSelect={(accountId) => update({ mode, accountId })} />
  else if (mode === 'card') body = <CardSource id={typeof props.cardId === 'string' ? props.cardId : null} editable={editable} onSelect={(cardId) => update({ mode, cardId })} />
  else body = <FinanceExpenses month={props.month} editable={editable} update={update} />

  return <section contentEditable={false} aria-label={t('notes.financeBlock.title')} className="flex flex-col gap-3 rounded-lg border border-solid border-[var(--ant-color-border-secondary)] p-4">
    <div className="flex flex-wrap items-center gap-2">
      <strong>{t('notes.financeBlock.title')}</strong>
      {editable && Object.values(access).some(Boolean) ? <Select aria-label={t('notes.financeBlock.source')} value={mode} className="min-w-48"
        options={(['account', 'card', 'expenses'] as const).map((value) => ({ value, label: t(`notes.financeBlock.${value}`), disabled: !access[value] }))}
        onChange={(value) => update({ mode: value })} /> : <Typography.Text type="secondary">{t(`notes.financeBlock.${mode}`)}</Typography.Text>}
      {access[mode] && <Button type="text" size="small" icon={<ReloadOutlined />} aria-label={t('notes.financeBlock.refresh')} loading={refreshing} onClick={() => void refresh()}>{t('notes.financeBlock.refresh')}</Button>}
    </div>
    {body}
  </section>
}

function AccountSource({ id, editable, onSelect }: { id: string | null; editable: boolean; onSelect: (id: string) => void }) {
  const { t } = useTranslation()
  const query = useAccounts()
  if (query.isError) return <Alert type="error" title={toErrorMessage(query.error, t('notes.financeBlock.error'))} />
  if (query.isLoading) return <Spin size="small" />
  const active = (query.data ?? []).filter((account) => !account.archivedAt)
  const selected = active.find((account) => account.id === id)
  return <>
    {editable ? <Select aria-label={t('notes.financeBlock.accountSelect')} value={selected?.id} placeholder={t('notes.financeBlock.chooseAccount')}
      showSearch={{ optionFilterProp: 'label' }} options={active.map((account) => ({ value: account.id, label: account.name }))} onChange={onSelect} />
      : selected && <Typography.Text strong>{selected.name}</Typography.Text>}
    {selected ? <><AccountFigures id={selected.id} /><Link to={`/finances/accounts/${selected.id}`}>{t('notes.financeBlock.open')}</Link></>
      : <Typography.Text type="secondary">{t(id ? 'notes.financeBlock.missingAccount' : 'notes.financeBlock.chooseAccount')}</Typography.Text>}
  </>
}

function AccountFigures({ id }: { id: string }) {
  const { t } = useTranslation()
  const query = useAccountBalance(id)
  if (query.isError) return <Alert type="error" title={toErrorMessage(query.error, t('notes.financeBlock.error'))} />
  if (!query.data) return <Spin size="small" />
  return <div className="flex flex-wrap gap-3">
    <StatTile label={t('finances.accounts.postedBalance')} value={<span className="tabular-nums">{formatMoney(query.data.posted, query.data.currency)}</span>} />
    <StatTile label={t('finances.accounts.projectedBalance')} value={<span className="tabular-nums">{formatMoney(query.data.projected, query.data.currency)}</span>} />
  </div>
}

function CardSource({ id, editable, onSelect }: { id: string | null; editable: boolean; onSelect: (id: string) => void }) {
  const { t } = useTranslation()
  const query = useCards()
  if (query.isError) return <Alert type="error" title={toErrorMessage(query.error, t('notes.financeBlock.error'))} />
  if (query.isLoading) return <Spin size="small" />
  const active = (query.data ?? []).filter((card) => !card.archivedAt)
  const selected = active.find((card) => card.id === id)
  return <>
    {editable ? <Select aria-label={t('notes.financeBlock.cardSelect')} value={selected?.id} placeholder={t('notes.financeBlock.chooseCard')}
      showSearch={{ optionFilterProp: 'label' }} options={active.map((card) => ({ value: card.id, label: card.name }))} onChange={onSelect} />
      : selected && <Typography.Text strong>{selected.name}</Typography.Text>}
    {selected ? <><CardFigures id={selected.id} currency={selected.currency} /><Link to={`/finances/cards/${selected.id}`}>{t('notes.financeBlock.open')}</Link></>
      : <Typography.Text type="secondary">{t(id ? 'notes.financeBlock.missingCard' : 'notes.financeBlock.chooseCard')}</Typography.Text>}
  </>
}

function CardFigures({ id, currency }: { id: string; currency: string }) {
  const { t } = useTranslation()
  const query = useCardAvailableLimit(id)
  if (query.isError) return <Alert type="error" title={toErrorMessage(query.error, t('notes.financeBlock.error'))} />
  if (!query.data) return <Spin size="small" />
  const money = (value: number | null) => value == null ? t('notes.financeBlock.notDefined') : <span className="tabular-nums">{formatMoney(value, currency)}</span>
  return <div className="flex flex-wrap gap-3">
    <StatTile label={t('finances.cards.creditLimit')} value={money(query.data.creditLimit)} />
    <StatTile label={t('finances.cards.availableLimit')} value={money(query.data.availableLimit)} />
  </div>
}
