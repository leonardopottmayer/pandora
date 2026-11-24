import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Descriptions, Drawer, Empty, Spin, Tag, Typography, theme } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import type { PositionMovementDto } from '../models'
import { formatDate, formatMoney } from '../lib/format'
import { assetClassLabel, rateLabel } from '../lib/investments'
import { useAccountNames } from '../hooks/useAccounts'
import { useAssetClassMap, usePosition, useUndoPositionMovement } from '../hooks/useInvestments'
import { CurrencyAmount } from './CurrencyAmount'
import { MovementFormModal } from './MovementFormModal'
import { ReportValueModal } from './ReportValueModal'

/** Direction of the cash a movement moved, for the amount's color. */
function cashDirection(m: PositionMovementDto): 'in' | 'out' | undefined {
  if (m.netAmount == null) return undefined
  return m.kind === 'buy' ? 'out' : 'in'
}

/** One position: its data, movements and reported values, with its actions in the footer. */
export function PositionDetailDrawer({ positionId, onClose }: { positionId: string | null; onClose: () => void }) {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { token } = theme.useToken()
  const { data, isLoading } = usePosition(positionId)
  const classes = useAssetClassMap()
  const accountNames = useAccountNames()
  const undo = useUndoPositionMovement()
  const [moving, setMoving] = useState(false)
  const [valuing, setValuing] = useState(false)

  const p = data?.position
  const open = p?.status === 'open'
  const latest = data?.movements.at(-1)

  function confirmUndo() {
    if (!p || !latest) return
    modal.confirm({
      title: t('finances.investments.undoConfirm'),
      content: (
        <div className="flex flex-col gap-2">
          <span>{`${t(`finances.investments.movementKinds.${latest.kind}`)} · ${formatDate(latest.occurredOn)}`}</span>
          <Typography.Text type="secondary">
            {data?.movements.length === 1 ? t('finances.investments.undoDeletesHint') : t('finances.investments.undoHint')}
          </Typography.Text>
        </div>
      ),
      okText: t('finances.investments.undo'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          const stillThere = await undo.mutateAsync(p.id)
          message.success(t('finances.investments.undone'))
          if (!stillThere) onClose()
        } catch (err) {
          message.error(toErrorMessage(err, t('finances.investments.saveError')))
        }
      },
    })
  }

  const divider = `1px solid ${token.colorBorderSecondary}`
  const sectionTitle = (text: string) => (
    <Typography.Text strong type="secondary" className="text-xs uppercase tracking-wider">
      {text}
    </Typography.Text>
  )

  return (
    <Drawer
      open={!!positionId}
      title={p ? (p.assetCode ? `${p.assetCode} · ${p.assetName}` : p.assetName) : t('finances.investments.position')}
      size={480}
      onClose={onClose}
      destroyOnHidden
      footer={
        p && (
          <div className="flex flex-wrap items-center gap-2">
            {latest && (
              <Button danger onClick={confirmUndo}>
                {t('finances.investments.undoLatest')}
              </Button>
            )}
            {open && <Button onClick={() => setValuing(true)}>{t('finances.investments.reportValue')}</Button>}
            {open && (
              <Button type="primary" className="ms-auto" onClick={() => setMoving(true)}>
                {t('finances.investments.newMovement')}
              </Button>
            )}
          </div>
        )
      }
    >
      {isLoading || !p || !data ? (
        <Spin />
      ) : (
        <div className="flex flex-col gap-5">
          <div className="flex flex-col gap-1">
            <span className="flex items-center gap-2">
              <span className="text-2xl font-semibold tabular-nums">{formatMoney(p.currentValue, p.currency)}</span>
              {!open && <Tag>{t('finances.investments.closed')}</Tag>}
            </span>
            <Typography.Text type="secondary" className="text-xs">
              {p.valuedOn ? t('finances.investments.valueOn', { date: formatDate(p.valuedOn) }) : t('finances.investments.noValue')}
            </Typography.Text>
          </div>

          <Descriptions
            size="small"
            column={1}
            items={[
              { key: 'account', label: t('finances.investments.account'), children: accountNames.get(p.accountId) },
              { key: 'class', label: t('finances.investments.assetClass'), children: assetClassLabel(classes.get(p.assetClassId), t) },
              ...(p.behavior === 'unit'
                ? [
                    { key: 'quantity', label: t('finances.investments.quantity'), children: p.quantity?.toLocaleString() },
                    { key: 'averageCost', label: t('finances.investments.averageCost'), children: formatMoney(p.averageCost ?? 0, p.currency) },
                  ]
                : [
                    { key: 'invested', label: t('finances.investments.investedAmount'), children: formatMoney(p.investedAmount ?? 0, p.currency) },
                    ...(p.appliedOn ? [{ key: 'applied', label: t('finances.investments.appliedOn'), children: formatDate(p.appliedOn) }] : []),
                    ...(p.index ? [{ key: 'rate', label: t('finances.investments.rate'), children: rateLabel(p, t) }] : []),
                    ...(p.maturityOn ? [{ key: 'maturity', label: t('finances.investments.maturityOn'), children: formatDate(p.maturityOn) }] : []),
                  ]),
              { key: 'cost', label: t('finances.investments.costBasis'), children: formatMoney(p.costBasis, p.currency) },
            ]}
          />

          <div className="flex flex-col gap-2">
            {sectionTitle(t('finances.investments.movements'))}
            <div className="overflow-hidden rounded-xl" style={{ border: divider }}>
              {[...data.movements].reverse().map((m, index) => (
                <div key={m.id} className="flex items-center gap-3 px-3 py-2" style={{ borderTop: index === 0 ? undefined : divider }}>
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate">{t(`finances.investments.movementKinds.${m.kind}`)}</span>
                    <Typography.Text type="secondary" className="truncate text-xs">
                      {[
                        formatDate(m.occurredOn),
                        m.quantity != null && `${m.quantity.toLocaleString()} × ${formatMoney(m.unitPrice ?? 0, p.currency)}`,
                        m.incomeKind && t(`finances.investments.incomeKinds.${m.incomeKind}`),
                        m.realizedResult != null && t('finances.investments.realized', { amount: formatMoney(m.realizedResult, p.currency) }),
                      ].filter(Boolean).join(' · ')}
                    </Typography.Text>
                  </span>
                  {m.netAmount != null && (
                    <span className="shrink-0 tabular-nums">
                      <CurrencyAmount amount={m.netAmount} currency={p.currency} direction={cashDirection(m)} />
                    </span>
                  )}
                </div>
              ))}
            </div>
          </div>

          <div className="flex flex-col gap-2">
            {sectionTitle(t('finances.investments.valueHistory'))}
            {data.valuations.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('finances.investments.noValues')} />
            ) : (
              <div className="overflow-hidden rounded-xl" style={{ border: divider }}>
                {data.valuations.map((v, index) => (
                  <div key={v.id} className="flex items-center justify-between px-3 py-2" style={{ borderTop: index === 0 ? undefined : divider }}>
                    <span className="tabular-nums">{formatDate(v.valuedOn)}</span>
                    <span className="tabular-nums">{formatMoney(v.value, p.currency)}</span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {moving && p && <MovementFormModal position={p} onClose={() => setMoving(false)} />}
      {valuing && p && <ReportValueModal position={p} onClose={() => setValuing(false)} />}
    </Drawer>
  )
}
