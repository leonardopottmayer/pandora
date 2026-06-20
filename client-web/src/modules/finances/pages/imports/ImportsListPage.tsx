import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import {
  App,
  Button,
  Card,
  Flex,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
  Upload,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { CloudUploadOutlined, ReloadOutlined, StopOutlined } from '@ant-design/icons'
import type { UploadProps } from 'antd'
import { toErrorMessage } from '@/lib/api/envelope'
import type { ImportFileDto } from '../../models'
import { useAccounts } from '../../hooks/useAccounts'
import { useCards } from '../../hooks/useCards'
import { useAbortImportFile, useImportFiles, useRetryImportFile, useUploadImportFile } from '../../hooks/useImports'

const { Text } = Typography

const STATUS_COLORS: Record<string, string> = {
  received: 'blue',
  parsing: 'processing',
  completed: 'green',
  failed: 'red',
  aborted: 'default',
}

export function ImportsListPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const navigate = useNavigate()

  const [destinationId, setDestinationId] = useState<string | undefined>()
  const [destinationType, setDestinationType] = useState<'account' | 'card'>('account')

  const { data, isLoading } = useImportFiles({ take: 100 })
  const { data: accounts } = useAccounts()
  const { data: cards } = useCards()
  const uploadMutation = useUploadImportFile()
  const abortMutation = useAbortImportFile()
  const retryMutation = useRetryImportFile()

  const accountOptions = (accounts ?? []).map((a) => ({ value: a.id, label: `${a.name} (${a.currency})` }))
  const cardOptions = (cards ?? []).map((c) => ({ value: c.id, label: `${c.name} (${c.currency})` }))

  const uploadProps: UploadProps = {
    accept: '.ofx,.csv',
    showUploadList: false,
    beforeUpload: async (file) => {
      if (!destinationId) {
        message.warning(t('finances.imports.selectDestinationFirst'))
        return false
      }
      try {
        const accountId = destinationType === 'account' ? destinationId : undefined
        const cardId = destinationType === 'card' ? destinationId : undefined
        await uploadMutation.mutateAsync({ file, accountId, cardId })
        message.success(t('finances.imports.uploaded'))
      } catch (err) {
        message.error(toErrorMessage(err, t('finances.imports.uploadError')))
      }
      return false
    },
  }

  async function handleAbort(id: string) {
    try {
      await abortMutation.mutateAsync(id)
      message.success(t('finances.imports.aborted'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.imports.actionError')))
    }
  }

  async function handleRetry(id: string) {
    try {
      await retryMutation.mutateAsync(id)
      message.success(t('finances.imports.retried'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.imports.actionError')))
    }
  }

  const columns: ColumnsType<ImportFileDto> = [
    {
      title: t('finances.imports.fileName'),
      dataIndex: 'fileName',
      render: (name, row) => (
        <a onClick={() => navigate(`/finances/imports/${row.id}`)}>{name}</a>
      ),
    },
    {
      title: t('finances.imports.status'),
      dataIndex: 'status',
      render: (status) => (
        <Tag color={STATUS_COLORS[status] ?? 'default'}>
          {t(`finances.imports.statuses.${status}`)}
        </Tag>
      ),
    },
    {
      title: t('finances.imports.rows'),
      render: (_, row) =>
        row.status === 'completed' ? (
          <Text>
            {row.parsedRows}/{row.totalRows}
            {row.duplicateRows > 0 && (
              <Text type="secondary"> ({row.duplicateRows} dup)</Text>
            )}
          </Text>
        ) : (
          '—'
        ),
    },
    {
      title: t('finances.imports.createdAt'),
      dataIndex: 'createdAt',
      render: (v: string) => new Date(v).toLocaleString(),
    },
    {
      title: t('common.actions'),
      render: (_, row) => (
        <Space>
          {row.status === 'failed' && (
            <Tooltip title={t('finances.imports.retry')}>
              <Button
                size="small"
                icon={<ReloadOutlined />}
                onClick={() => handleRetry(row.id)}
                loading={retryMutation.isPending}
              />
            </Tooltip>
          )}
          {(row.status === 'received' || row.status === 'parsing' || row.status === 'failed') && (
            <Tooltip title={t('finances.imports.abort')}>
              <Button
                size="small"
                danger
                icon={<StopOutlined />}
                onClick={() => handleAbort(row.id)}
                loading={abortMutation.isPending}
              />
            </Tooltip>
          )}
        </Space>
      ),
    },
  ]

  return (
    <Card
      title={t('finances.imports.title')}
      extra={
        <Space>
          <Select
            style={{ width: 110 }}
            value={destinationType}
            options={[
              { value: 'account', label: t('finances.imports.account') },
              { value: 'card', label: t('finances.imports.card') },
            ]}
            onChange={(v) => {
              setDestinationType(v)
              setDestinationId(undefined)
            }}
          />
          <Select
            style={{ width: 220 }}
            placeholder={
              destinationType === 'account'
                ? t('finances.imports.selectAccount')
                : t('finances.imports.selectCard')
            }
            options={destinationType === 'account' ? accountOptions : cardOptions}
            value={destinationId}
            onChange={setDestinationId}
            allowClear
          />
          <Upload {...uploadProps}>
            <Button
              type="primary"
              icon={<CloudUploadOutlined />}
              loading={uploadMutation.isPending}
            >
              {t('finances.imports.upload')}
            </Button>
          </Upload>
        </Space>
      }
    >
      <Flex vertical gap={16}>
        <Table<ImportFileDto>
          rowKey="id"
          columns={columns}
          dataSource={data}
          loading={isLoading}
          pagination={false}
          size="small"
        />
      </Flex>
    </Card>
  )
}
