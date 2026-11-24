import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Button, Empty, Skeleton, Space, Typography, theme } from 'antd'
import { PlusOutlined, SyncOutlined } from '@ant-design/icons'
import { todayIn } from '@/lib/today'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { PageHeading } from '@/components/settings/PageHeading'
import type { InvestmentGroupDto, PositionDto } from '../../models'
import { formatDate, formatMoney } from '../../lib/format'
import { assetClassLabel } from '../../lib/investments'
import { StatTile } from '../../components/StatTile'
import { PositionRow } from '../../components/PositionRow'
import { PositionDetailDrawer } from '../../components/PositionDetailDrawer'
import { useAccountNames } from '../../hooks/useAccounts'
import { useAssetClassMap, useInvestmentsSummary, usePositions } from '../../hooks/useInvestments'
import { PositionFormModal } from './PositionFormModal'
import { UpdateValuesModal } from './UpdateValuesModal'

/** What is invested, where and in what: totals per currency, allocation, and the open positions by account. */
export function InvestmentsPage() {
  const { t } = useTranslation()
  const { timeZone } = usePreferences()
  const { token } = theme.useToken()
  const navigate = useNavigate()
  const [creating, setCreating] = useState(false)
  const [updating, setUpdating] = useState(false)
  const [openId, setOpenId] = useState<string | null>(null)

  const { data: summary } = useInvestmentsSummary()
  const { data: positions, isLoading } = usePositions()
  const classes = useAssetClassMap()
  const accountNames = useAccountNames()
  const today = todayIn(timeZone)

  const totalOf = new Map((summary?.totals ?? []).map((total) => [total.currency, total.value]))
  const next = summary?.upcomingMaturities[0]

  const byAccount = new Map<string, PositionDto[]>()
  for (const p of positions ?? []) byAccount.set(p.accountId, [...(byAccount.get(p.accountId) ?? []), p])
  const groups = [...byAccount].sort(([a], [b]) => (accountNames.get(a) ?? '').localeCompare(accountNames.get(b) ?? ''))

  const divider = `1px solid ${token.colorBorderSecondary}`
  const panel = { border: divider, background: token.colorBgContainer }

  const groupHeading = (label: ReactNode, key: string) => (
    <div
      key={key}
      className="sticky top-0 z-10 flex justify-between gap-3 border-x-0 border-b border-t border-solid px-4 py-1.5 text-xs font-semibold uppercase tracking-wider first:border-t-0"
      style={{
        color: token.colorTextSecondary,
        borderColor: token.colorBorderSecondary,
        // Opaque, so the rows going under it do not show through; tinted like a table header.
        background: token.colorBgContainer,
        boxShadow: `inset 0 0 0 9999px ${token.colorFillAlter}`,
      }}
    >
      {label}
    </div>
  )

  /** One allocation list: each slice with its value and its share of its currency's total. */
  const allocation = (title: string, items: InvestmentGroupDto[], name: (id: string) => string) => (
    <div className="flex min-w-0 flex-col gap-2">
      <Typography.Text strong type="secondary" className="px-0.5 text-xs uppercase tracking-wider">
        {title}
      </Typography.Text>
      <div className="overflow-hidden rounded-xl" style={panel}>
        {items.length === 0 ? (
          <Typography.Text type="secondary" className="block px-4 py-2.5">—</Typography.Text>
        ) : (
          items.map((item, index) => {
            const share = (totalOf.get(item.currency) ?? 0) > 0 ? item.value / totalOf.get(item.currency)! : 0
            return (
              <div key={`${item.id}-${item.currency}`} className="flex items-center gap-3 px-4 py-2" style={{ borderTop: index === 0 ? undefined : divider }}>
                <span className="min-w-0 flex-1 truncate">{name(item.id)}</span>
                <span className="w-12 shrink-0 text-right text-xs tabular-nums" style={{ color: token.colorTextTertiary }}>
                  {Math.round(share * 100)}%
                </span>
                <span className="w-32 shrink-0 text-right tabular-nums">{formatMoney(item.value, item.currency)}</span>
              </div>
            )
          })
        )}
      </div>
    </div>
  )

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('nav.investments')} description={t('finances.investments.subtitle')} />
        <Space wrap>
          <Button onClick={() => navigate('/finances/investments/classes')}>{t('finances.investments.classesButton')}</Button>
          <Button icon={<PlusOutlined />} onClick={() => setCreating(true)}>
            {t('finances.investments.newPosition')}
          </Button>
          {/* The routine task once positions exist: the values are reported, not fetched (INV-4). */}
          <Button type="primary" icon={<SyncOutlined />} disabled={!positions?.length} onClick={() => setUpdating(true)}>
            {t('finances.investments.updateValues')}
          </Button>
        </Space>
      </div>

      <div className="flex flex-wrap gap-3">
        {(summary?.totals.length ?? 0) === 0 ? (
          <StatTile label={t('finances.investments.invested')} value="—" />
        ) : (
          summary!.totals.map((total) => (
            <StatTile
              key={total.currency}
              label={`${t('finances.investments.invested')} · ${total.currency}`}
              value={<span className="tabular-nums">{formatMoney(total.value, total.currency)}</span>}
              note={t('finances.investments.costNote', { amount: formatMoney(total.cost, total.currency) })}
            />
          ))
        )}
        <StatTile
          label={t('finances.investments.nextMaturity')}
          value={next ? <span className="tabular-nums">{formatDate(next.maturityOn)}</span> : '—'}
          note={next && `${next.assetCode ?? next.assetName} · ${formatMoney(next.currentValue, next.currency)}`}
        />
      </div>

      <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
        {allocation(t('finances.investments.byClass'), summary?.byClass ?? [], (id) => assetClassLabel(classes.get(id), t))}
        {allocation(t('finances.investments.byAccount'), summary?.byAccount ?? [], (id) => accountNames.get(id) ?? '')}
      </div>

      <div className="overflow-hidden rounded-xl" style={panel}>
        {isLoading ? (
          <Skeleton active className="p-4" />
        ) : groups.length === 0 ? (
          <Empty className="my-8" image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('finances.investments.empty')} />
        ) : (
          groups.flatMap(([accountId, items]) => [
            groupHeading(
              <>
                <span className="truncate">{`${accountNames.get(accountId) ?? ''} · ${items.length}`}</span>
                <span className="shrink-0 tabular-nums">
                  {formatMoney(items.reduce((sum, p) => sum + p.currentValue, 0), items[0].currency)}
                </span>
              </>,
              `heading-${accountId}`,
            ),
            ...items.map((p, index) => (
              <PositionRow
                key={p.id}
                position={p}
                assetClass={classes.get(p.assetClassId)}
                today={today}
                first={index === 0}
                onOpen={setOpenId}
              />
            )),
          ])
        )}
      </div>

      <PositionFormModal open={creating} onClose={() => setCreating(false)} />
      {updating && <UpdateValuesModal positions={positions ?? []} onClose={() => setUpdating(false)} />}
      <PositionDetailDrawer positionId={openId} onClose={() => setOpenId(null)} />
    </div>
  )
}
