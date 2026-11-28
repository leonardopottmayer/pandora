import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, AutoComplete, Form, Input, Modal, Select } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import type { AssetDto } from '../../models'
import { COMMON_CURRENCIES } from '../../lib/enums'
import { assetClassLabel } from '../../lib/investments'
import { useAssetClasses, useCreateAsset, useUpdateAsset } from '../../hooks/useInvestments'

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
  /** The asset to edit; omitted to create one. */
  asset?: AssetDto
  onClose: () => void
  onCreated?: (asset: AssetDto) => void
}

/**
 * Registers or edits an asset: a stock, a CDB product, a coin. The currency is fixed once created; the class
 * can change only to one with the same behavior, since positions follow the behavior.
 */
export function AssetFormModal({ open, defaultCurrency = 'BRL', asset, onClose, onCreated }: AssetFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<AssetFormValues>()
  const { data: classes } = useAssetClasses()
  const create = useCreateAsset()
  const update = useUpdateAsset()
  const behavior = asset ? classes?.find((c) => c.id === asset.assetClassId)?.behavior : undefined

  useEffect(() => {
    if (!open) return
    form.resetFields()
    form.setFieldsValue(
      asset
        ? {
            assetClassId: asset.assetClassId,
            name: asset.name,
            code: asset.code ?? undefined,
            issuer: asset.issuer ?? undefined,
            isin: asset.isin ?? undefined,
            currency: asset.currency,
          }
        : { currency: defaultCurrency },
    )
  }, [open, defaultCurrency, asset, form])

  async function handleSubmit(values: AssetFormValues) {
    const fields = {
      name: values.name.trim(),
      code: values.code?.trim() || null,
      issuer: values.issuer?.trim() || null,
      isin: values.isin?.trim() || null,
    }
    try {
      if (asset) {
        await update.mutateAsync({ id: asset.id, body: { ...fields, assetClassId: values.assetClassId } })
        message.success(t('finances.investments.assetSaved'))
      } else {
        const created = await create.mutateAsync({ ...fields, assetClassId: values.assetClassId, currency: values.currency })
        message.success(t('finances.investments.assetCreated'))
        onCreated?.(created)
      }
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.investments.saveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={asset ? t('finances.investments.editAsset') : t('finances.investments.newAsset')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: create.isPending || update.isPending }}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <Form.Item
          name="assetClassId"
          label={t('finances.investments.assetClass')}
          extra={asset ? t('finances.investments.sameBehaviorHint') : undefined}
          rules={[{ required: true, message: t('finances.investments.required.assetClass') }]}
        >
          <Select
            showSearch={{ optionFilterProp: 'label' }}
            options={(classes ?? [])
              .filter((c) => !c.archivedAt || c.id === asset?.assetClassId)
              .filter((c) => !behavior || c.behavior === behavior)
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
            <AutoComplete disabled={!!asset} options={COMMON_CURRENCIES.map((c) => ({ value: c }))} />
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
