import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, DatePicker, Empty, InputNumber, Modal, Typography, theme } from 'antd'
import type { Dayjs } from 'dayjs'
import { todayIn } from '@/lib/today'
import { toErrorMessage } from '@/lib/api/envelope'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import type { PositionDto } from '../../models'
import { formatDate, formatMoney } from '../../lib/format'
import { useAccountNames } from '../../hooks/useAccounts'
import { useReportPositionValues } from '../../hooks/useInvestments'

interface UpdateValuesModalProps {
  positions: PositionDto[]
  onClose: () => void
}

/**
 * The routine update: every open position in one list, with the bank's or broker's app open beside it.
 * Only the rows given a value are sent, all on the same date. Mounted only while open, so it starts empty.
 */
export function UpdateValuesModal({ positions, onClose }: UpdateValuesModalProps) {
  const { t } = useTranslation()
  const { timeZone } = usePreferences()
  const { message } = App.useApp()
  const { token } = theme.useToken()
  const accountNames = useAccountNames()
  const report = useReportPositionValues()
  const [valuedOn, setValuedOn] = useState<Dayjs>(() => todayIn(timeZone))
  const [values, setValues] = useState<Record<string, number | null>>({})

  const items = Object.entries(values)
    .filter((entry): entry is [string, number] => entry[1] != null)
    .map(([positionId, value]) => ({ positionId, value }))

  async function handleSave() {
    try {
      await report.mutateAsync({ valuedOn: valuedOn.format('YYYY-MM-DD'), items })
      message.success(t('finances.investments.valuesSaved', { count: items.length }))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.investments.saveError')))
    }
  }

  const sorted = [...positions].sort((a, b) =>
    (accountNames.get(a.accountId) ?? '').localeCompare(accountNames.get(b.accountId) ?? '') || a.assetName.localeCompare(b.assetName))
  const divider = `1px solid ${token.colorBorderSecondary}`

  return (
    <Modal
      open
      title={t('finances.investments.updateValues')}
      width={720}
      onCancel={onClose}
      onOk={() => void handleSave()}
      okButtonProps={{ loading: report.isPending, disabled: items.length === 0 }}
      okText={items.length > 0 ? t('finances.investments.saveValues', { count: items.length }) : t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
      styles={{ body: { maxHeight: 'calc(100dvh - 260px)', overflowY: 'auto', paddingRight: 8 } }}
    >
      <div className="flex flex-col gap-4">
        <div className="flex flex-wrap items-center gap-3">
          <Typography.Text>{t('finances.investments.valuesOn')}</Typography.Text>
          <DatePicker
            value={valuedOn}
            onChange={(date) => date && setValuedOn(date)}
            allowClear={false}
            format="DD/MM/YYYY"
            aria-label={t('finances.investments.valuesOn')}
          />
          <Typography.Text type="secondary" className="text-xs">
            {t('finances.investments.updateValuesHint')}
          </Typography.Text>
        </div>

        {sorted.length === 0 ? (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('finances.investments.empty')} />
        ) : (
          <div className="overflow-hidden rounded-xl" style={{ border: divider }}>
            {sorted.map((p, index) => (
              <div
                key={p.id}
                className="flex flex-wrap items-center gap-3 px-4 py-2 sm:flex-nowrap"
                style={{ borderTop: index === 0 ? undefined : divider }}
              >
                <span className="flex min-w-0 flex-1 flex-col">
                  <span className="truncate">{p.assetCode ? `${p.assetCode} · ${p.assetName}` : p.assetName}</span>
                  <Typography.Text type="secondary" className="truncate text-xs">
                    {[
                      accountNames.get(p.accountId),
                      t('finances.investments.lastValue', {
                        amount: formatMoney(p.currentValue, p.currency),
                        date: p.valuedOn ? formatDate(p.valuedOn) : t('finances.investments.noValue'),
                      }),
                    ].filter(Boolean).join(' · ')}
                  </Typography.Text>
                </span>
                <InputNumber
                  className="w-full sm:w-44"
                  min={0}
                  step={0.01}
                  prefix={p.currency}
                  placeholder={p.currentValue.toFixed(2)}
                  value={values[p.id] ?? null}
                  onChange={(value) => setValues((current) => ({ ...current, [p.id]: value }))}
                  aria-label={t('finances.investments.newValueFor', { name: p.assetCode ?? p.assetName })}
                />
              </div>
            ))}
          </div>
        )}
      </div>
    </Modal>
  )
}
