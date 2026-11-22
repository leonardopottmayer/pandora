import { App, Button, Form, Input, Modal, Select, Space, Switch } from 'antd'
import { FolderOpenOutlined } from '@ant-design/icons'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { toErrorMessage } from '@/lib/api/envelope'
import type { Device } from '@/modules/identity/models'
import { filesKeys, useFilesAgent } from '../hooks/useFiles'
import { shortTime } from '../lib/format'
import * as filesService from '../services/files.service'
import type { Root } from '../models'

type CaseChoice = 'default' | 'yes' | 'no'

interface FormValues {
  deviceId: string
  localPath: string
  name: string
  daily: boolean
  time: string
  includeHidden: boolean
  caseSensitive: CaseChoice
}

/** Adds a root to a device, or edits a root's settings (`root` set). The device and path of a root never change. */
export function RootFormModal({
  open,
  root,
  devices,
  onClose,
}: {
  open: boolean
  root: Root | null
  devices: Device[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const { agent, deviceId: thisDeviceId } = useFilesAgent()
  const [form] = Form.useForm<FormValues>()
  const chosenDevice = Form.useWatch('deviceId', form)
  const daily = Form.useWatch('daily', form)

  const save = useMutation({
    mutationFn: async (v: FormValues) => {
      const settings = {
        name: v.name?.trim() || v.localPath,
        scanTime: v.daily ? `${v.time}:00` : null,
        includeHidden: v.includeHidden,
        caseSensitive: v.caseSensitive === 'default' ? null : v.caseSensitive === 'yes',
      }
      if (root) return filesService.updateRoot(root.id, { ...settings, name: v.name?.trim() || root.name })
      return filesService.addRoot({ ...settings, deviceId: v.deviceId, localPath: v.localPath.trim() })
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: filesKeys.all })
      message.success(t('files.roots.saved'))
      onClose()
    },
    onError: (e) => message.error(toErrorMessage(e, t('files.roots.saveError'))),
  })

  async function pickFolder() {
    const { path } = await agent!.invoke<{ path: string | null }>('files.pickFolder')
    if (!path) return
    form.setFieldValue('localPath', path)
    if (!form.getFieldValue('name')) form.setFieldValue('name', path.replace(/[\\/]+$/, '').split(/[\\/]/).pop() || path)
  }

  const initialValues: FormValues = root
    ? {
        deviceId: root.deviceId,
        localPath: root.localPath,
        name: root.name,
        daily: root.scanTime !== null,
        time: root.scanTime ? shortTime(root.scanTime) : '03:00',
        includeHidden: root.includeHidden,
        caseSensitive: root.caseSensitive ? 'yes' : 'no',
      }
    : {
        deviceId: thisDeviceId && devices.some((d) => d.id === thisDeviceId) ? thisDeviceId : (devices[0]?.id ?? ''),
        localPath: '',
        name: '',
        daily: true,
        time: '03:00',
        includeHidden: false,
        caseSensitive: 'default',
      }

  return (
    <Modal
      open={open}
      title={root ? t('files.roots.editTitle') : t('files.roots.addTitle')}
      okText={t('common.save')}
      onOk={() => form.submit()}
      confirmLoading={save.isPending}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" initialValues={initialValues} onFinish={(v) => save.mutate(v)} preserve={false}>
        {!root && (
          <>
            <Form.Item name="deviceId" label={t('files.roots.device')} rules={[{ required: true }]}>
              <Select options={devices.map((d) => ({ value: d.id, label: d.name }))} />
            </Form.Item>
            <Form.Item
              label={t('files.roots.localPath')}
              extra={agent && chosenDevice === thisDeviceId ? null : t('files.roots.localPathTyped')}
              required
            >
              <Space.Compact className="w-full">
                <Form.Item name="localPath" noStyle rules={[{ required: true, whitespace: true }]}>
                  <Input placeholder="E:\" />
                </Form.Item>
                {agent && chosenDevice === thisDeviceId && (
                  <Button icon={<FolderOpenOutlined />} onClick={() => void pickFolder()}>
                    {t('files.roots.pick')}
                  </Button>
                )}
              </Space.Compact>
            </Form.Item>
          </>
        )}
        <Form.Item name="name" label={t('files.roots.name')}>
          <Input />
        </Form.Item>
        <Form.Item label={t('files.roots.schedule')} extra={t('files.roots.scheduleDesc')}>
          <Space>
            <Form.Item name="daily" valuePropName="checked" noStyle>
              <Switch checkedChildren={t('files.roots.daily')} unCheckedChildren={t('files.roots.manual')} />
            </Form.Item>
            {daily && (
              <Form.Item name="time" noStyle rules={[{ required: true }]}>
                <Input type="time" aria-label={t('files.roots.time')} />
              </Form.Item>
            )}
          </Space>
        </Form.Item>
        <Form.Item name="includeHidden" label={t('files.roots.includeHidden')} valuePropName="checked">
          <Switch />
        </Form.Item>
        <Form.Item name="caseSensitive" label={t('files.roots.caseSensitive')}>
          <Select
            options={[
              ...(root ? [] : [{ value: 'default', label: t('files.roots.caseDefault') }]),
              { value: 'no', label: t('files.roots.caseNo') },
              { value: 'yes', label: t('files.roots.caseYes') },
            ]}
          />
        </Form.Item>
      </Form>
    </Modal>
  )
}
