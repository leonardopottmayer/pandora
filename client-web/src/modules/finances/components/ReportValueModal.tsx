import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { App, DatePicker, Form, InputNumber, Modal } from 'antd'
import type { Dayjs } from 'dayjs'
import { todayIn } from '@/lib/today'
import { toErrorMessage } from '@/lib/api/envelope'
import { usePreferences } from '@/modules/identity/context/preferences-context'
import type { PositionDto } from '../models'
import { useReportPositionValue } from '../hooks/useInvestments'

/** What one position is worth on a date, as the bank or broker shows it. */
export function ReportValueModal({ position, onClose }: { position: PositionDto | null; onClose: () => void }) {
  const { t } = useTranslation()
  const { timeZone } = usePreferences()
  const { message } = App.useApp()
  const [form] = Form.useForm<{ valuedOn: Dayjs; value: number }>()
  const report = useReportPositionValue()

  useEffect(() => {
    if (!position) return
    form.setFieldsValue({ valuedOn: todayIn(timeZone), value: position.currentValue })
  }, [position, form, timeZone])

  if (!position) return null

  async function handleSubmit(values: { valuedOn: Dayjs; value: number }) {
    if (!position) return
    try {
      await report.mutateAsync({ id: position.id, valuedOn: values.valuedOn.format('YYYY-MM-DD'), value: values.value })
      message.success(t('finances.investments.valueSaved'))
      onClose()
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.investments.saveError')))
    }
  }

  return (
    <Modal
      open
      title={t('finances.investments.reportValue')}
      onCancel={onClose}
      onOk={() => form.submit()}
      okButtonProps={{ loading: report.isPending }}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleSubmit}>
        <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
          <Form.Item name="valuedOn" label={t('finances.investments.date')} rules={[{ required: true }]}>
            <DatePicker style={{ width: '100%' }} format="DD/MM/YYYY" />
          </Form.Item>
          <Form.Item
            name="value"
            label={t('finances.investments.currentValue')}
            rules={[{ required: true, message: t('finances.investments.required.value') }]}
          >
            <InputNumber style={{ width: '100%' }} min={0} step={0.01} prefix={position.currency} autoFocus />
          </Form.Item>
        </div>
      </Form>
    </Modal>
  )
}
