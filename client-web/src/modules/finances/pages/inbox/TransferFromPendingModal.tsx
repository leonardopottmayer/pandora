import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, DatePicker, Descriptions, Input, Modal } from 'antd'
import dayjs from 'dayjs'
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
  const { t } = useTranslation()
  const [description, setDescription] = useState('')
  const [occurredOn, setOccurredOn] = useState(dayjs())

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
    >
      {!valid ? (
        <Alert type="warning" showIcon message={t('finances.inbox.transferInvalid')} />
      ) : (
        <>
          <Descriptions column={1} size="small" bordered className="mb-3">
            <Descriptions.Item label={t('finances.transactions.fromAccount')}>
              {outflow!.accountId ? accountNames.get(outflow!.accountId) ?? '—' : '—'}
            </Descriptions.Item>
            <Descriptions.Item label={t('finances.transactions.toAccount')}>
              {inflow!.accountId ? accountNames.get(inflow!.accountId) ?? '—' : '—'}
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
          <label className="mb-1 block">{t('finances.transactions.description')}</label>
          <Input
            value={description}
            maxLength={255}
            onChange={(e) => setDescription(e.target.value)}
            className="mb-3"
          />
          <label className="mb-1 block">{t('finances.transactions.occurredOn')}</label>
          <DatePicker
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
