import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, DatePicker, Form, Input, InputNumber, Modal, Select } from 'antd'
import type { Dayjs } from 'dayjs'
import { todayIn } from '@/lib/today'
import { toErrorMessage } from '@/lib/api/envelope'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { INCOME_KINDS, type IncomeKind, type PositionDto, type PositionMovementKind } from '../models'
import { movementKindsFor } from '../lib/investments'
import { useAddPositionMovement } from '../hooks/useInvestments'

interface MovementFormValues {
  kind: PositionMovementKind
  occurredOn: Dayjs
  quantity?: number
  unitPrice?: number
  grossAmount?: number
  costs?: number
  taxWithheld?: number
  incomeKind?: IncomeKind
  splitFactor?: number
  notes?: string
}

/** Which fields each kind asks for. */
const FIELDS: Record<string, (keyof MovementFormValues)[]> = {
  buy: ['quantity', 'unitPrice', 'costs'],
  sell: ['quantity', 'unitPrice', 'costs', 'taxWithheld'],
  redemption: ['grossAmount', 'taxWithheld', 'costs'],
  maturity: ['grossAmount', 'taxWithheld', 'costs'],
  income: ['incomeKind', 'grossAmount', 'taxWithheld'],
  split: ['splitFactor'],
  'come-cotas': ['quantity', 'taxWithheld'],
}

/** A movement on an existing position; the fields follow the kind. */
export function MovementFormModal({ position, onClose }: { position: PositionDto | null; onClose: () => void }) {
  const { t } = useTranslation()
  const { timeZone } = usePreferences()
  const { message } = App.useApp()
  const [form] = Form.useForm<MovementFormValues>()
  const add = useAddPositionMovement()
  const kind = Form.useWatch('kind', form)
  const fields = FIELDS[kind ?? ''] ?? []
  const has = (field: keyof MovementFormValues) => fields.includes(field)

  useEffect(() => {
    if (!position) return
    form.resetFields()
    form.setFieldsValue({
      kind: movementKindsFor(position.behavior)[0],
      occurredOn: todayIn(timeZone),
      costs: 0,
      taxWithheld: 0,
    })
  }, [position, form, timeZone])

  if (!position) return null
  const money = { style: { width: '100%' }, min: 0, step: 0.01, prefix: position.currency }

  async function handleSubmit(values: MovementFormValues) {
    if (!position) return
    const pick = <K extends keyof MovementFormValues>(field: K) => (has(field) ? values[field] ?? null : null)
    try {
      await add.mutateAsync({
        id: position.id,
        body: {
          kind: values.kind,
          occurredOn: values.occurredOn.format('YYYY-MM-DD'),
          quantity: pick('quantity') as number | null,
          unitPrice: pick('unitPrice') as number | null,
          grossAmount: pick('grossAmount') as number | null,
          costs: (pick('costs') as number | null) ?? 0,
          taxWithheld: (pick('taxWithheld') as number | null) ?? 0,
          incomeKind: pick('incomeKind') as IncomeKind | null,
          splitFactor: pick('splitFactor') as number | null,
          notes: values.notes?.trim() || null,
        },
      })
      message.success(t('finances.investments.movementSaved'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.investments.saveError')))
    }
  }

  const required = (field: string) => [{ required: true, message: t(`finances.investments.required.${field}`) }]

  return (
    <Modal
      open
      title={t('finances.investments.newMovement')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: add.isPending }}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
          <Form.Item name="kind" label={t('finances.investments.movementKind')}>
            <Select
              options={movementKindsFor(position.behavior).map((k) => ({ value: k, label: t(`finances.investments.movementKinds.${k}`) }))}
            />
          </Form.Item>
          <Form.Item name="occurredOn" label={t('finances.investments.date')} rules={required('date')}>
            <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
          </Form.Item>
        </div>
        {has('incomeKind') && (
          <Form.Item name="incomeKind" label={t('finances.investments.incomeKind')} rules={required('incomeKind')}>
            <Select options={INCOME_KINDS.map((k) => ({ value: k, label: t(`finances.investments.incomeKinds.${k}`) }))} />
          </Form.Item>
        )}
        <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
          {has('quantity') && (
            <Form.Item name="quantity" label={t('finances.investments.quantity')} rules={required('quantity')}>
              <InputNumber style={{ width: '100%' }} min={0} />
            </Form.Item>
          )}
          {has('unitPrice') && (
            <Form.Item name="unitPrice" label={t('finances.investments.unitPrice')} rules={required('unitPrice')}>
              <InputNumber {...money} />
            </Form.Item>
          )}
          {has('grossAmount') && (
            <Form.Item name="grossAmount" label={t('finances.investments.grossAmount')} rules={required('grossAmount')}>
              <InputNumber {...money} />
            </Form.Item>
          )}
          {has('splitFactor') && (
            <Form.Item
              name="splitFactor"
              label={t('finances.investments.splitFactor')}
              extra={t('finances.investments.splitFactorHint')}
              rules={required('splitFactor')}
            >
              <InputNumber style={{ width: '100%' }} min={0} />
            </Form.Item>
          )}
          {has('costs') && (
            <Form.Item name="costs" label={t('finances.investments.costs')}>
              <InputNumber {...money} />
            </Form.Item>
          )}
          {has('taxWithheld') && (
            <Form.Item name="taxWithheld" label={t('finances.investments.taxWithheld')}>
              <InputNumber {...money} />
            </Form.Item>
          )}
        </div>
        <Form.Item name="notes" label={t('finances.investments.notes')}>
          <Input maxLength={255} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
