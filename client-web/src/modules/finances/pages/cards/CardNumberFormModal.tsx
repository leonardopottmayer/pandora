import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Form, Input, Modal, Radio } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import type { CardNumberDto, CardNumberKind } from '../../models'
import { useSaveCardNumber } from '../../hooks/useCards'

interface CardNumberFormValues {
  kind: CardNumberKind
  lastFour: string
  nickname?: string
}

interface CardNumberFormModalProps {
  open: boolean
  cardId: string
  /** The number being edited; absent to add one. */
  number?: CardNumberDto | null
  onClose: () => void
}

export function CardNumberFormModal({ open, cardId, number, onClose }: CardNumberFormModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<CardNumberFormValues>()
  const save = useSaveCardNumber()

  useEffect(() => {
    if (!open) return
    form.resetFields()
    form.setFieldsValue(number
      ? { kind: number.kind, lastFour: number.lastFour, nickname: number.nickname ?? undefined }
      : { kind: 'virtual' })
  }, [open, number, form])

  async function handleSubmit(values: CardNumberFormValues) {
    try {
      await save.mutateAsync({
        cardId,
        numberId: number?.id,
        body: { kind: values.kind, lastFour: values.lastFour.trim(), nickname: values.nickname?.trim() || null },
      })
      message.success(t('finances.cards.numberSaved'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.cards.numberSaveError')))
    }
  }

  return (
    <Modal
      open={open}
      title={number ? t('finances.cards.editNumber') : t('finances.cards.newNumber')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: save.isPending }}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <Form.Item name="kind" label={t('finances.cards.numberKind')}>
          <Radio.Group
            optionType="button"
            options={(['physical', 'virtual'] as const).map((kind) => ({ value: kind, label: t(`finances.cards.numberKinds.${kind}`) }))}
          />
        </Form.Item>
        <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
          <Form.Item
            name="lastFour"
            label={t('finances.cards.lastFour')}
            rules={[{ required: true, pattern: /^\d{4}$/, message: t('finances.cards.lastFourInvalid') }]}
          >
            <Input maxLength={4} inputMode="numeric" autoFocus />
          </Form.Item>
          <Form.Item name="nickname" label={t('finances.cards.nickname')} extra={t('finances.cards.nicknameHint')}>
            <Input maxLength={50} />
          </Form.Item>
        </div>
      </Form>
    </Modal>
  )
}
