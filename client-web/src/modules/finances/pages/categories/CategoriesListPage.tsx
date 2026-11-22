import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, Tabs } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { PageHeading } from '@/components/settings/PageHeading'
import { toErrorMessage } from '@/lib/api/envelope'
import type { UserCategoryDto } from '../../models'
import { useSetUserCategoryActive, useSystemCategories, useUserCategories } from '../../hooks/useCategories'
import { CategoryFormModal } from './CategoryFormModal'
import { CategoryListTab } from './CategoryListTab'

export function CategoriesListPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const [includeInactive, setIncludeInactive] = useState(false)
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<UserCategoryDto | null>(null)
  const { data: userCategories, isLoading } = useUserCategories(includeInactive)
  const { data: systemCategories, isLoading: loadingSystem } = useSystemCategories()
  const activeMutation = useSetUserCategoryActive()

  function openEdit(category: UserCategoryDto) {
    setEditing(category)
    setModalOpen(true)
  }

  async function toggleActive(category: UserCategoryDto) {
    const active = !category.isActive
    try {
      await activeMutation.mutateAsync({ id: category.id, active })
      message.success(active ? t('finances.categories.activated') : t('finances.categories.deactivated'))
    } catch (err) {
      message.error(toErrorMessage(err, t('finances.categories.saveError')))
    }
  }

  return (
    <div className="flex min-w-0 flex-col gap-6 lg:h-[calc(100dvh-112px)]">
      <div className="flex shrink-0 flex-wrap items-start justify-between gap-3">
        <PageHeading title={t('nav.categories')} description={t('finances.categories.subtitle')} />
        <Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditing(null); setModalOpen(true) }}>{t('finances.categories.new')}</Button>
      </div>
      <Tabs
        className="min-h-0 flex-1 [&_.ant-tabs-content]:lg:h-full" defaultActiveKey="mine" destroyOnHidden={false} animated={false}
        items={[
          { key: 'mine', label: t('finances.categories.userTitle'), className: 'lg:h-full', children: (
            <CategoryListTab categories={userCategories ?? []} loading={isLoading} includeInactive={includeInactive} onIncludeInactiveChange={setIncludeInactive}
              onEdit={openEdit} onToggleActive={toggleActive} changingActive={activeMutation.isPending} />
          ) },
          { key: 'system', label: t('finances.categories.systemTitle'), className: 'lg:h-full', children: (
            <CategoryListTab categories={systemCategories ?? []} loading={loadingSystem} />
          ) },
        ]}
      />
      <CategoryFormModal open={modalOpen} category={editing} onClose={() => setModalOpen(false)} />
    </div>
  )
}
