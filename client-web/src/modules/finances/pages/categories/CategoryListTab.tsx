import { useState, type Key } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Dropdown, Input, Select, Switch, Table, Tag, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { EllipsisOutlined, SearchOutlined } from '@ant-design/icons'
import { TRANSACTION_NATURES, type TransactionNature } from '../../models'
import { TRANSACTION_NATURE_META } from '../../lib/enums'
import { countCategories, filterCategoryTree } from './categoryTree'

interface CategoryNode<T> {
  id: string
  name: string
  nature: TransactionNature
  isActive: boolean
  children: T[]
}

interface CategoryListTabProps<T> {
  categories: T[]
  loading: boolean
  includeInactive?: boolean
  onIncludeInactiveChange?: (value: boolean) => void
  onEdit?: (category: T) => void
  onToggleActive?: (category: T) => void
  changingActive?: boolean
}

/** Shared hierarchy and filters for personal categories and the read-only system catalog. */
export function CategoryListTab<T extends CategoryNode<T>>({
  categories, loading, includeInactive, onIncludeInactiveChange, onEdit, onToggleActive, changingActive,
}: CategoryListTabProps<T>) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [text, setText] = useState('')
  const [nature, setNature] = useState<TransactionNature>()
  const [expandedKeys, setExpandedKeys] = useState<readonly Key[]>([])
  const [scrollEl, setScrollEl] = useState<HTMLDivElement | null>(null)
  const filtered = filterCategoryTree(categories, text, nature)
  const searching = text.trim().length > 0
  const hasFilters = searching || nature != null || !!includeInactive
  const columns: ColumnsType<T> = [{
    title: t('finances.categories.name'),
    dataIndex: 'name',
    render: (name: string, category) => (
      <div className="inline-flex max-w-[calc(100%-44px)] min-w-0 flex-col align-middle">
        <span className="flex min-w-0 items-center gap-2">
          <span className="truncate" title={name} style={!category.isActive ? { color: token.colorTextTertiary } : undefined}>{name}</span>
          {!category.isActive && <Tag>{t('finances.categories.inactive')}</Tag>}
        </span>
        <Typography.Text type="secondary" className="text-xs">{t(TRANSACTION_NATURE_META[category.nature].labelKey)}</Typography.Text>
      </div>
    ),
  }]
  if (onEdit && onToggleActive) columns.push({
    key: 'actions', width: 48, align: 'right',
    render: (_, category) => (
      <span onClick={(event) => event.stopPropagation()}>
        <Dropdown trigger={['click']} placement="bottomRight" menu={{ items: [
          { key: 'edit', label: t('common.edit'), onClick: () => onEdit(category) },
          { key: 'active', label: t(category.isActive ? 'finances.categories.deactivate' : 'finances.categories.activate'), disabled: changingActive, onClick: () => onToggleActive(category) },
        ] }}>
          <Button type="text" size="small" icon={<EllipsisOutlined />} aria-label={t('finances.categories.moreActions', { name: category.name })} />
        </Dropdown>
      </span>
    ),
  })

  return (
    <div className="flex min-h-0 min-w-0 flex-col gap-4 lg:h-full">
      {!onEdit && <Typography.Text type="secondary" className="shrink-0">{t('finances.categories.systemDesc')}</Typography.Text>}
      <div className="flex shrink-0 flex-wrap items-center gap-2">
        <div className="w-full sm:w-64">
          <Input allowClear prefix={<SearchOutlined />} value={text} onChange={(event) => setText(event.target.value)} placeholder={t('finances.categories.search')} />
        </div>
        <Select<TransactionNature> allowClear className="w-full sm:w-44" aria-label={t('finances.categories.nature')} placeholder={t('finances.categories.nature')} value={nature} onChange={setNature}
          options={TRANSACTION_NATURES.map((value) => ({ value, label: t(TRANSACTION_NATURE_META[value].labelKey) }))} />
        {onIncludeInactiveChange && <label className="flex cursor-pointer items-center gap-2 px-1 text-sm">
          <Switch size="small" checked={includeInactive} onChange={onIncludeInactiveChange} aria-label={t('finances.categories.showInactive')} />
          {t('finances.categories.showInactive')}
        </label>}
        {hasFilters && <Button type="link" onClick={() => { setText(''); setNature(undefined); onIncludeInactiveChange?.(false) }}>{t('finances.filters.clear')}</Button>}
      </div>
      <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl" style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorBgContainer }}>
        <div ref={setScrollEl} className="min-h-0 flex-1 overflow-auto">
          <Table<T>
            rowKey="id" dataSource={filtered.map((category) => ({ ...category, subcategories: category.children.length ? category.children : undefined }))} loading={loading} columns={columns} size="middle" tableLayout="fixed" pagination={false}
            sticky={scrollEl ? { getContainer: () => scrollEl } : undefined}
            expandable={{
              childrenColumnName: 'subcategories',
              expandedRowKeys: searching ? filtered.filter((category) => category.children.length).map((category) => category.id) : expandedKeys,
              onExpandedRowsChange: setExpandedKeys,
              indentSize: 16,
            }}
            onRow={onEdit ? (category) => ({ onClick: () => onEdit(category), className: 'cursor-pointer' }) : undefined}
            locale={{ emptyText: t(onEdit ? 'finances.categories.empty' : 'finances.categories.emptySystem') }}
          />
        </div>
        <Typography.Text type="secondary" className="shrink-0 px-4 py-2 text-xs tabular-nums" style={{ borderTop: `1px solid ${token.colorBorderSecondary}` }}>
          {t('finances.categories.count', { count: countCategories(filtered) })}
        </Typography.Text>
      </div>
    </div>
  )
}
