import { useId, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, DatePicker, Descriptions, Input, Modal } from 'antd'
import dayjs from 'dayjs'
import { todayIn } from '@/lib/today'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import type { PendingTransactionDto } from '../../models'
import { kindDirection } from '../../lib/enums'
import { CurrencyAmount } from '../../components/CurrencyAmount'

interface TransferFromPendingModalProps {
  open: boolean
  /** The two selected suggestions; direction is resolved by the parent. */
  outflow: PendingTransactionDto | null
  inflow: PendingTransactionDto | null
  accountNames: Map<string, string>
  loading?: boolean
  onClose: () => void
  onConfirm: (description: string, occurredOn: string) => void
}

/** Confirms turning an outflow + inflow suggestion into a transfer pair. */
export function TransferFromPendingModal({
  open,
  outflow,
  inflow,
  accountNames,
  loading,
  onClose,
  onConfirm,
}: TransferFromPendingModalProps) {
  const { timeZone } = usePreferences()
  const { t } = useTranslation()
  const descriptionId = useId()
  const dateId = useId()
  const [description, setDescription] = useState('')
  const [occurredOn, setOccurredOn] = useState(() => todayIn(timeZone))

  // Seed the editable fields from the outflow leg when the modal opens.
  // Adjusting state during render (guarded by an open-transition check) instead
  // of in an effect avoids a cascading re-render and satisfies
  // react-hooks/set-state-in-effect.
  const [prevOpen, setPrevOpen] = useState(false)
  if (open !== prevOpen) {
    setPrevOpen(open)
    if (open && outflow) {
      setDescription(outflow.description)
      setOccurredOn(dayjs(outflow.occurredOn))
    }
  }

  const valid = !!outflow && !!inflow

  return (
    <Modal
      open={open}
      title={t('finances.inbox.transferTitle')}
      onCancel={onClose}
      onOk={() => onConfirm(description, occurredOn.format('YYYY-MM-DD'))}
      okText={t('finances.inbox.transferConfirm')}
      okButtonProps={{ disabled: !valid || !description.trim(), loading }}
      cancelText={t('common.cancel')}
      destroyOnHidden
      width={640}
      styles={{ body: { maxHeight: 'calc(100dvh - 260px)', overflowY: 'auto', paddingRight: 8 } }}
    >
      {!valid ? (
        <Alert type="warning" showIcon title={t('finances.inbox.transferInvalid')} />
      ) : (
        <>
          <Descriptions column={1} size="small" bordered className="mb-3">
            <Descriptions.Item label={t('finances.transactions.fromAccount')}>
              <span className="break-words">{outflow!.accountId ? accountNames.get(outflow!.accountId) ?? '—' : '—'}</span>
            </Descriptions.Item>
            <Descriptions.Item label={t('finances.transactions.toAccount')}>
              <span className="break-words">{inflow!.accountId ? accountNames.get(inflow!.accountId) ?? '—' : '—'}</span>
            </Descriptions.Item>
            <Descriptions.Item label={t('finances.transactions.amountOut')}>
              {outflow!.amount != null && (
                <CurrencyAmount
                  amount={outflow!.amount}
                  currency={outflow!.currency}
                  direction={kindDirection(outflow!.kind)}
                />
              )}
            </Descriptions.Item>
            <Descriptions.Item label={t('finances.transactions.amountIn')}>
              {inflow!.amount != null && (
                <CurrencyAmount
                  amount={inflow!.amount}
                  currency={inflow!.currency}
                  direction={kindDirection(inflow!.kind)}
                />
              )}
            </Descriptions.Item>
          </Descriptions>
          <label htmlFor={descriptionId} className="mb-1 block">{t('finances.transactions.description')}</label>
          <Input
            id={descriptionId}
            value={description}
            maxLength={255}
            onChange={(e) => setDescription(e.target.value)}
            className="mb-3"
          />
          <label htmlFor={dateId} className="mb-1 block">{t('finances.transactions.occurredOn')}</label>
          <DatePicker
            id={dateId}
            format="DD/MM/YYYY"
            value={occurredOn}
            onChange={(d) => d && setOccurredOn(d)}
            allowClear={false}
            className="w-full"
          />
        </>
      )}
    </Modal>
  )
}
