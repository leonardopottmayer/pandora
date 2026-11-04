import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Input, Select, Table, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { SearchOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import type { ImportLayoutDto } from '../../models'
import { useImportLayouts } from '../../hooks/useImports'

export function ImportLayoutsPage() {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const { data: layouts = [], isLoading, isError, error } = useImportLayouts()
  const [text, setText] = useState('')
  const [format, setFormat] = useState<string>()
  const [accountType, setAccountType] = useState<string>()
  const [scrollEl, setScrollEl] = useState<HTMLDivElement | null>(null)
  const destination = (value: string) => value === 'card' ? t('finances.imports.card') : t('finances.imports.account')
  const formats = [...new Set(layouts.map((layout) => layout.fileFormat))].sort()
  const destinations = [...new Set(layouts.map((layout) => layout.accountType))].sort()
  const filtered = useMemo(() => {
    const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase().trim()
    const search = normalize(text)
    return layouts.filter((layout) => (!format || layout.fileFormat === format)
      && (!accountType || layout.accountType === accountType)
      && normalize(`${layout.name} ${layout.bankName ?? ''} ${layout.bankCode ?? ''} ${layout.layoutCode}`).includes(search))
  }, [layouts, text, format, accountType])
  const bank = (layout: ImportLayoutDto) => [layout.bankName, layout.bankCode].filter(Boolean).join(' · ') || '—'
  const columns: ColumnsType<ImportLayoutDto> = [
    {
      title: t('finances.importLayouts.name'), key: 'name',
      render: (_, layout) => <div className="flex min-w-0 flex-col gap-1">
        <span className="truncate" title={layout.name}>{layout.name}</span>
        <Typography.Text type="secondary" className="truncate text-xs md:hidden" title={bank(layout)}>{bank(layout)}</Typography.Text>
        <Typography.Text type="secondary" className="text-xs md:hidden">{layout.fileFormat.toUpperCase()} · {destination(layout.accountType)}</Typography.Text>
        <Typography.Text type="secondary" className="truncate font-mono text-xs" title={layout.layoutCode}>{layout.layoutCode}</Typography.Text>
      </div>,
    },
    {
      title: t('finances.importLayouts.bank'), key: 'bank', responsive: ['md'],
      render: (_, layout) => <div className="min-w-0">
        <div className="truncate" title={layout.bankName ?? undefined}>{layout.bankName ?? '—'}</div>
        {layout.bankCode && <Typography.Text type="secondary" className="text-xs tabular-nums">{layout.bankCode}</Typography.Text>}
      </div>,
    },
    { title: t('finances.importLayouts.format'), dataIndex: 'fileFormat', width: 100, responsive: ['md'], render: (value: string) => value.toUpperCase() },
    { title: t('finances.importLayouts.accountType'), dataIndex: 'accountType', width: 140, responsive: ['md'], render: destination },
  ]

  return <div className="mx-auto flex w-full min-w-0 max-w-5xl flex-col gap-6 lg:h-[calc(100dvh-112px)]">
    <div className="shrink-0"><PageHeading title={t('finances.importLayouts.title')} description={t('finances.importLayouts.subtitle')} /></div>
    <div className="flex shrink-0 flex-wrap items-center gap-2">
      <div className="w-full sm:w-72"><Input allowClear prefix={<SearchOutlined />} placeholder={t('finances.importLayouts.search')} value={text} onChange={(event) => setText(event.target.value)} /></div>
      <Select className="w-full sm:w-36" allowClear value={format} onChange={setFormat} aria-label={t('finances.importLayouts.format')} placeholder={t('finances.importLayouts.format')}
        options={formats.map((value) => ({ value, label: value.toUpperCase() }))} />
      <Select className="w-full sm:w-44" allowClear value={accountType} onChange={setAccountType} aria-label={t('finances.importLayouts.accountType')} placeholder={t('finances.importLayouts.accountType')}
        options={destinations.map((value) => ({ value, label: destination(value) }))} />
      {(text || format || accountType) && <Button type="link" onClick={() => { setText(''); setFormat(undefined); setAccountType(undefined) }}>{t('finances.filters.clear')}</Button>}
    </div>
    {isError && <Alert type="error" showIcon title={error.message} className="shrink-0" />}
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl" style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}>
      <div ref={setScrollEl} className="min-h-0 flex-1 overflow-auto">
        <Table<ImportLayoutDto> rowKey="id" columns={columns} dataSource={filtered} loading={isLoading} pagination={false} size="middle" tableLayout="fixed"
          sticky={scrollEl ? { getContainer: () => scrollEl } : undefined}
          locale={{ emptyText: t(layouts.length ? 'finances.importLayouts.noMatches' : 'finances.importLayouts.empty') }} />
      </div>
      <Typography.Text type="secondary" className="shrink-0 px-4 py-2 text-xs tabular-nums" style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}>{t('finances.importLayouts.count', { count: filtered.length })}</Typography.Text>
    </div>
  </div>
}
