import { useTranslation } from 'react-i18next'
import { App, DatePicker, Form, Modal, Radio, Select, Upload } from 'antd'
import type { UploadFile } from 'antd'
import type { Dayjs } from 'dayjs'
import { CloudUploadOutlined } from '@ant-design/icons'
import { toErrorMessage } from '@/lib/api/envelope'
import { useUploadImportFile } from '../../hooks/useImports'

interface ImportUploadModalProps {
  onClose: () => void
  accountOptions: { value: string; label: string }[]
  cardOptions: { value: string; label: string }[]
  loadingDestinations: boolean
}
interface UploadValues {
  destinationType: 'account' | 'card'
  destinationId: string
  files: UploadFile[]
  cutoffDate?: Dayjs | null
}

export function ImportUploadModal({ onClose, accountOptions, cardOptions, loadingDestinations }: ImportUploadModalProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [form] = Form.useForm<UploadValues>()
  const destinationType = Form.useWatch('destinationType', form) ?? 'account'
  const uploadMutation = useUploadImportFile()

  async function submit(values: UploadValues) {
    try {
      await uploadMutation.mutateAsync({
        file: values.files[0].originFileObj!,
        accountId: values.destinationType === 'account' ? values.destinationId : undefined,
        cardId: values.destinationType === 'card' ? values.destinationId : undefined,
        cutoffDate: values.cutoffDate?.format('YYYY-MM-DD'),
      })
      message.success(t('finances.imports.uploaded'))
      onClose()
    } catch (error) {
      message.error(toErrorMessage(error, t('finances.imports.uploadError')))
    }
  }

  return <Modal open title={t('finances.imports.upload')} onCancel={onClose} onOk={() => form.submit()}
    okText={t('finances.imports.upload')} cancelText={t('common.cancel')} okButtonProps={{ loading: uploadMutation.isPending }}
    cancelButtonProps={{ disabled: uploadMutation.isPending }} closable={!uploadMutation.isPending} keyboard={!uploadMutation.isPending}
    mask={{ closable: !uploadMutation.isPending }} destroyOnHidden
    styles={{ body: { maxHeight: 'calc(100dvh - 260px)', overflowY: 'auto', paddingRight: 8 } }}>
    <Form form={form} layout="vertical" initialValues={{ destinationType: 'account', files: [] }} onFinish={submit}>
      <Form.Item name="destinationType" label={t('finances.imports.destination')}>
        <Radio.Group onChange={() => form.setFieldValue('destinationId', undefined)} options={[
          { value: 'account', label: t('finances.imports.account') }, { value: 'card', label: t('finances.imports.card') },
        ]} />
      </Form.Item>
      <Form.Item name="destinationId" label={t(destinationType === 'account' ? 'finances.imports.account' : 'finances.imports.card')}
        rules={[{ required: true, message: t('finances.imports.selectDestinationFirst') }]}>
        <Select allowClear showSearch={{ optionFilterProp: 'label' }} loading={loadingDestinations}
          options={destinationType === 'account' ? accountOptions : cardOptions}
          placeholder={t(destinationType === 'account' ? 'finances.imports.selectAccount' : 'finances.imports.selectCard')} />
      </Form.Item>
      <Form.Item name="files" label={t('finances.imports.fileName')} valuePropName="fileList"
        getValueFromEvent={(event: { fileList: UploadFile[] }) => event.fileList}
        rules={[{ required: true, type: 'array', min: 1, message: t('finances.imports.chooseFile') }]}>
        <Upload.Dragger accept=".ofx,.csv" maxCount={1} beforeUpload={() => false} disabled={uploadMutation.isPending}>
          <p className="ant-upload-drag-icon"><CloudUploadOutlined /></p>
          <p className="ant-upload-text">{t('finances.imports.dropFile')}</p>
          <p className="ant-upload-hint">{t('finances.imports.formats')}</p>
        </Upload.Dragger>
      </Form.Item>
      <details>
        <summary className="mb-3 w-fit cursor-pointer select-none text-sm">{t('finances.imports.dateOptions')}</summary>
        <Form.Item name="cutoffDate" label={t('finances.imports.cutoffDate')} extra={t('finances.imports.cutoffHint')}>
          <DatePicker className="w-full" format="DD/MM/YYYY" placeholder={t('finances.imports.cutoffDate')} />
        </Form.Item>
      </details>
    </Form>
  </Modal>
}
