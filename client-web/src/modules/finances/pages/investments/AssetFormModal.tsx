import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, AutoComplete, Form, Input, Modal, Select } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import type { AssetDto } from '../../models'
import { COMMON_CURRENCIES } from '../../lib/enums'
import { assetClassLabel } from '../../lib/investments'
import { useAssetClasses, useCreateAsset } from '../../hooks/useInvestments'

interface AssetFormValues {
  assetClassId: string
  name: string
  code?: string
  issuer?: string
  currency: string
  isin?: string
}

interface AssetFormModalProps {
  open: boolean
  /** The currency of the account the asset is for. */
  defaultCurrency?: string
  onClose: () => void
  onCreated?: (asset: AssetDto) => void
}

/** Registers an asset: a stock, a CDB product, a coin. */
export function AssetFormModal({ open, defaultCurrency = 'BRL', onClose, onCreated }: AssetFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<AssetFormValues>()
  const { data: classes } = useAssetClasses()
  const create = useCreateAsset()

  useEffect(() => {
    if (!open) return
    form.resetFields()
    form.setFieldsValue({ currency: defaultCurrency })
  }, [open, defaultCurrency, form])

  async function handleSubmit(values: AssetFormValues) {
    try {
      const asset = await create.mutateAsync({
        assetClassId: values.assetClassId,
        name: values.name.trim(),
        code: values.code?.trim() || null,
        issuer: values.issuer?.trim() || null,
        isin: values.isin?.trim() || null,
        currency: values.currency,
      })
      message.success(t('finances.investments.assetCreated'))
      onCreated?.(asset)
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.investments.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={t('finances.investments.newAsset')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: create.isPending }}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <Form.Item
          name="assetClassId"
          label={t('finances.investments.assetClass')}
          rules={[{ required: true, message: t('finances.investments.required.assetClass') }]}
        >
          <Select
            showSearch={{ optionFilterProp: 'label' }}
            options={(classes ?? [])
              .filter((c) => !c.archivedAt)
              .map((c) => ({ value: c.id, label: assetClassLabel(c, t) }))}
          />
        </Form.Item>
        <Form.Item
          name="name"
          label={t('finances.investments.assetName')}
          extra={t('finances.investments.assetNameHint')}
          rules={[{ required: true, message: t('finances.investments.required.assetName') }]}
        >
          <Input maxLength={100} autoFocus />
        </Form.Item>
        <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
          <Form.Item name="code" label={t('finances.investments.assetCode')}>
            <Input maxLength={20} placeholder="ITSA4" />
          </Form.Item>
          <Form.Item
            name="currency"
            label={t('finances.investments.currency')}
            normalize={(value: string) => value?.toUpperCase()}
            rules={[{ required: true }]}
          >
            <AutoComplete options={COMMON_CURRENCIES.map((c) => ({ value: c }))} />
          </Form.Item>
        </div>
        <details>
          <summary className="mb-3 w-fit cursor-pointer select-none text-sm">{t('finances.investments.issuerAndIsin')}</summary>
          <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
            <Form.Item name="issuer" label={t('finances.investments.issuer')}>
              <Input maxLength={100} />
            </Form.Item>
            <Form.Item name="isin" label="ISIN">
              <Input maxLength={12} />
            </Form.Item>
          </div>
        </details>
      </Form>
    </Modal>
  )
}
