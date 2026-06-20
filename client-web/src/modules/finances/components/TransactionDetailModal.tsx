import { useTranslation } from 'react-i18next'
import { Descriptions, Modal, Spin, Tag } from 'antd'
import { useTransaction } from '../hooks/useTransactions'
import { useAccountNames } from '../hooks/useAccounts'
import { useCardNames } from '../hooks/useCards'
import { useCategoryNames } from '../hooks/useCategories'
import { kindDirection, transactionKindLabelKey, TRANSACTION_STATUS_META } from '../lib/enums'
import { formatDate, formatReferenceMonth } from '../lib/format'
import { CurrencyAmount } from './CurrencyAmount'

interface TransactionDetailModalProps {
  transactionId: string | null
  open: boolean
  onClose: () => void
}

/** Read-only view of a single transaction, used to diagnose import duplicates without leaving the page. */
export function TransactionDetailModal({ transactionId, open, onClose }: TransactionDetailModalProps) {
  const { t } = useTranslation()
  const { data: tx, isLoading } = useTransaction(open ? transactionId : null)
  const accountNames = useAccountNames()
  const cardNames = useCardNames()
  const categoryNames = useCategoryNames()

  const categoryId = tx ? tx.systemCategoryId ?? tx.userCategoryId : null

  return (
    <Modal
      open={open}
      title={t('finances.transactions.detailTitle')}
      onCancel={onClose}
      footer={null}
      destroyOnHidden
    >
      {isLoading || !tx ? (
        <Spin style={{ display: 'block', margin: '32px auto' }} />
      ) : (
        <Descriptions column={1} size="small" bordered>
          <Descriptions.Item label={t('finances.transactions.occurredOn')}>
            {formatDate(tx.occurredOn)}
          </Descriptions.Item>
          <Descriptions.Item label={t('finances.transactions.description')}>
            {tx.description}
          </Descriptions.Item>
          <Descriptions.Item label={t('finances.transactions.kind')}>
            {t(transactionKindLabelKey(tx.kind))}
          </Descriptions.Item>
          <Descriptions.Item label={t('finances.transactions.amount')}>
            <CurrencyAmount amount={tx.amount} currency={tx.currency} direction={kindDirection(tx.kind)} />
          </Descriptions.Item>
          <Descriptions.Item label={t('finances.transactions.status')}>
            <Tag color={TRANSACTION_STATUS_META[tx.status].color}>
              {t(TRANSACTION_STATUS_META[tx.status].labelKey)}
            </Tag>
          </Descriptions.Item>
          <Descriptions.Item label={t('finances.transactions.origin')}>
            {tx.cardId
              ? cardNames.get(tx.cardId) ?? '—'
              : tx.accountId
                ? accountNames.get(tx.accountId) ?? '—'
                : '—'}
          </Descriptions.Item>
          <Descriptions.Item label={t('finances.transactions.category')}>
            {categoryId ? categoryNames.get(categoryId) ?? '—' : '—'}
          </Descriptions.Item>
          {tx.statementReferenceMonth && (
            <Descriptions.Item label={t('finances.transactions.statement')}>
              {formatReferenceMonth(tx.statementReferenceMonth)}
            </Descriptions.Item>
          )}
          {tx.payee && (
            <Descriptions.Item label={t('finances.transactions.payee')}>{tx.payee}</Descriptions.Item>
          )}
          {tx.notes && (
            <Descriptions.Item label={t('finances.transactions.notes')}>{tx.notes}</Descriptions.Item>
          )}
        </Descriptions>
      )}
    </Modal>
  )
}
