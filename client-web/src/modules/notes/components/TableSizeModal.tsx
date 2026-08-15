import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Flex, InputNumber, Modal } from 'antd'

interface TableSizeModalProps {
  onCancel: () => void
  onConfirm: (rows: number, columns: number) => void
}

const DEFAULT_ROWS = 3
const DEFAULT_COLUMNS = 3
/** Past this a table stops being readable as markdown source, which is what the document stays. */
const MAX_SIZE = 20

/**
 * Asks for the size of the table `/table` is about to insert. The row count includes the header,
 * which GFM requires: 5 rows means a header plus four rows of content.
 *
 * It is mounted only while it is open, so every `/table` starts from the default size rather than
 * from whatever the last one used.
 */
export function TableSizeModal({ onCancel, onConfirm }: TableSizeModalProps) {
  const { t } = useTranslation()
  const [rows, setRows] = useState(DEFAULT_ROWS)
  const [columns, setColumns] = useState(DEFAULT_COLUMNS)

  function confirm() {
    onConfirm(rows, columns)
  }

  return (
    <Modal
      open
      title={t('notes.tableSize.title')}
      okText={t('notes.tableSize.insert')}
      onOk={confirm}
      onCancel={onCancel}
    >
      <Flex gap={16} style={{ padding: '8px 0' }}>
        <label>
          {t('notes.tableSize.rows')}
          <InputNumber
            autoFocus
            min={1}
            max={MAX_SIZE}
            value={rows}
            // A cleared field keeps the last valid number: the dialog always has a size to insert.
            onChange={(value) => setRows(value ?? rows)}
            onPressEnter={confirm}
            style={{ display: 'block' }}
          />
        </label>
        <label>
          {t('notes.tableSize.columns')}
          <InputNumber
            min={1}
            max={MAX_SIZE}
            value={columns}
            onChange={(value) => setColumns(value ?? columns)}
            onPressEnter={confirm}
            style={{ display: 'block' }}
          />
        </label>
      </Flex>
      <span style={{ opacity: 0.65 }}>{t('notes.tableSize.hint')}</span>
    </Modal>
  )
}
