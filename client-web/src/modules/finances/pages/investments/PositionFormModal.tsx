import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, DatePicker, Divider, Form, Input, InputNumber, Modal, Radio, Select } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import type { Dayjs } from 'dayjs'
import { todayIn } from '@/lib/today'
import { toErrorMessage } from '@/lib/api/envelope'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import { FIXED_INCOME_INDEXES, type FixedIncomeIndex } from '../../models'
import { AccountSelect } from '../../components/AccountSelect'
import { useAccounts } from '../../hooks/useAccounts'
import { useAssetClassMap, useAssets, useCreatePosition } from '../../hooks/useInvestments'
import { AssetFormModal } from './AssetFormModal'

interface PositionFormValues {
  accountId: string
  assetId: string
  kind: 'opening' | 'buy'
  occurredOn: Dayjs
  quantity?: number
  averageCost?: number
  unitPrice?: number
  investedAmount?: number
  currentValue?: number
  costs?: number
  index?: FixedIncomeIndex
  indexPercent?: number
  spreadRate?: number
  maturityOn?: Dayjs
  notes?: string
}

interface PositionFormModalProps {
  open: boolean
  /** The account the screen was opened from. */
  defaultAccountId?: string
  onClose: () => void
}

/**
 * A new position and its first movement: an opening (what is already held, no cash moves, INV-6) or a
 * buy (the cash leaves the account now). The fields follow the asset's class: quantity and cost for
 * stocks and the like, amount, rate and maturity for fixed income.
 */
export function PositionFormModal({ open, defaultAccountId, onClose }: PositionFormModalProps) {
  const { t } = useTranslation()
  const { timeZone } = usePreferences()
  const { message } = App.useApp()
  const [form] = Form.useForm<PositionFormValues>()
  const [creatingAsset, setCreatingAsset] = useState(false)
  const { data: accounts } = useAccounts()
  const { data: assets } = useAssets()
  const classes = useAssetClassMap()
  const create = useCreatePosition()

  const accountId = Form.useWatch('accountId', form)
  const assetId = Form.useWatch('assetId', form)
  const kind = Form.useWatch('kind', form)
  const index = Form.useWatch('index', form)

  const currency = accounts?.find((a) => a.id === accountId)?.currency ?? 'BRL'
  const asset = assets?.find((a) => a.id === assetId)
  const behavior = asset ? classes.get(asset.assetClassId)?.behavior : undefined
  const opening = kind === 'opening'
  const money = { style: { width: '100%' }, min: 0, step: 0.01, prefix: currency }

  useEffect(() => {
    if (!open) return
    form.resetFields()
    form.setFieldsValue({ accountId: defaultAccountId, kind: 'opening', occurredOn: todayIn(timeZone), costs: 0 })
  }, [open, defaultAccountId, form, timeZone])

  async function handleSubmit(values: PositionFormValues) {
    const unit = behavior === 'unit'
    try {
      await create.mutateAsync({
        accountId: values.accountId,
        assetId: values.assetId,
        kind: values.kind,
        occurredOn: values.occurredOn.format('YYYY-MM-DD'),
        quantity: unit ? values.quantity ?? null : null,
        averageCost: unit && opening ? values.averageCost ?? null : null,
        unitPrice: unit && !opening ? values.unitPrice ?? null : null,
        investedAmount: unit ? null : values.investedAmount ?? null,
        currentValue: opening ? values.currentValue ?? null : null,
        costs: opening ? 0 : values.costs ?? 0,
        index: unit ? null : values.index ?? null,
        indexPercent: !unit && (values.index === 'cdi' || values.index === 'selic') ? values.indexPercent ?? null : null,
        spreadRate: !unit && values.index && values.index !== 'cdi' && values.index !== 'selic' ? values.spreadRate ?? null : null,
        maturityOn: unit ? null : values.maturityOn?.format('YYYY-MM-DD') ?? null,
        notes: values.notes?.trim() || null,
      })
      message.success(t('finances.investments.positionCreated'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.investments.saveError')))
    }
  }

  const required = (field: string) => [{ required: true, message: t(`finances.investments.required.${field}`) }]

  return (
    <>
      <Modal
        open={open}
        title={t('finances.investments.newPosition')}
        width={640}
        onCancel={onClose}
        onOk={() => form.submit()}
        okButtonProps={{ loading: create.isPending }}
        okText={t('common.save')}
        cancelText={t('common.cancel')}
        destroyOnHidden
        styles={{ body: { maxHeight: 'calc(100dvh - 260px)', overflowY: 'auto', paddingRight: 8 } }}
      >
        <Form form={form} layout="vertical" onFinish={handleSubmit}>
          <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
            <Form.Item name="accountId" label={t('finances.investments.account')} rules={required('account')}>
              <AccountSelect />
            </Form.Item>
            <Form.Item name="assetId" label={t('finances.investments.asset')} rules={required('asset')}>
              <Select
                showSearch={{ optionFilterProp: 'label' }}
                placeholder={t('finances.investments.assetPlaceholder')}
                options={(assets ?? [])
                  .filter((a) => !a.archivedAt && a.currency === currency)
                  .map((a) => ({ value: a.id, label: a.code ? `${a.code} · ${a.name}` : a.name }))}
                popupRender={(menu) => (
                  <>
                    {menu}
                    <Divider style={{ margin: '4px 0' }} />
                    <Button type="text" block icon={<PlusOutlined />} onClick={() => setCreatingAsset(true)}>
                      {t('finances.investments.newAsset')}
                    </Button>
                  </>
                )}
              />
            </Form.Item>
          </div>

          <Form.Item
            name="kind"
            label={t('finances.investments.startWith')}
            extra={t(opening ? 'finances.investments.openingHint' : 'finances.investments.buyHint')}
          >
            <Radio.Group
              optionType="button"
              options={[
                { value: 'opening', label: t('finances.investments.movementKinds.opening') },
                { value: 'buy', label: t('finances.investments.movementKinds.buy') },
              ]}
            />
          </Form.Item>

          {behavior && (
            <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
              <Form.Item
                name="occurredOn"
                label={t(behavior === 'unit' ? 'finances.investments.date' : 'finances.investments.appliedOn')}
                rules={required('date')}
              >
                <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
              </Form.Item>

              {behavior === 'unit' ? (
                <>
                  <Form.Item name="quantity" label={t('finances.investments.quantity')} rules={required('quantity')}>
                    <InputNumber style={{ width: '100%' }} min={0} />
                  </Form.Item>
                  {opening ? (
                    <Form.Item name="averageCost" label={t('finances.investments.averageCost')} rules={required('averageCost')}>
                      <InputNumber {...money} />
                    </Form.Item>
                  ) : (
                    <Form.Item name="unitPrice" label={t('finances.investments.unitPrice')} rules={required('unitPrice')}>
                      <InputNumber {...money} />
                    </Form.Item>
                  )}
                </>
              ) : (
                <>
                  <Form.Item name="investedAmount" label={t('finances.investments.investedAmount')} rules={required('investedAmount')}>
                    <InputNumber {...money} />
                  </Form.Item>
                  <Form.Item name="index" label={t('finances.investments.index')}>
                    <Select
                      allowClear
                      options={FIXED_INCOME_INDEXES.map((i) => ({ value: i, label: t(`finances.investments.indexes.${i}`) }))}
                    />
                  </Form.Item>
                  {(index === 'cdi' || index === 'selic') && (
                    <Form.Item name="indexPercent" label={t('finances.investments.indexPercent')}>
                      <InputNumber style={{ width: '100%' }} min={0} suffix="%" />
                    </Form.Item>
                  )}
                  {index && index !== 'cdi' && index !== 'selic' && (
                    <Form.Item
                      name="spreadRate"
                      label={t(index === 'pre' ? 'finances.investments.preRate' : 'finances.investments.spreadRate')}
                    >
                      <InputNumber style={{ width: '100%' }} min={0} suffix={t('finances.investments.perYear')} />
                    </Form.Item>
                  )}
                  <Form.Item name="maturityOn" label={t('finances.investments.maturityOn')}>
                    <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
                  </Form.Item>
                </>
              )}

              {opening ? (
                <Form.Item
                  name="currentValue"
                  label={t('finances.investments.currentValue')}
                  extra={t('finances.investments.currentValueHint')}
                >
                  <InputNumber {...money} />
                </Form.Item>
              ) : (
                <Form.Item name="costs" label={t('finances.investments.costs')}>
                  <InputNumber {...money} />
                </Form.Item>
              )}
            </div>
          )}

          {behavior && (
            <Form.Item name="notes" label={t('finances.investments.notes')}>
              <Input maxLength={255} />
            </Form.Item>
          )}
        </Form>
      </Modal>

      <AssetFormModal
        open={creatingAsset}
        defaultCurrency={currency}
        onClose={() => setCreatingAsset(false)}
        onCreated={(created) => form.setFieldValue('assetId', created.id)}
      />
    </>
  )
}
