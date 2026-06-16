import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { TreeSelect } from 'antd'
import type { SystemCategoryDto, TransactionNature, UserCategoryDto } from '../models'
import { useSystemCategories, useUserCategories } from '../hooks/useCategories'

/** Selected value: a system category OR a user category (mutually exclusive). */
export interface CategorySelection {
  systemCategoryId: string | null
  userCategoryId: string | null
}

interface CategorySelectProps {
  nature?: TransactionNature
  value?: CategorySelection
  onChange?: (value: CategorySelection) => void
  disabled?: boolean
}

interface TreeNode {
  value: string
  title: string
  children?: TreeNode[]
}

function systemNodes(cats: SystemCategoryDto[]): TreeNode[] {
  return cats.map((c) => ({
    value: `sys:${c.id}`,
    title: c.name,
    children: c.children?.length ? systemNodes(c.children) : undefined,
  }))
}

function userNodes(cats: UserCategoryDto[]): TreeNode[] {
  return cats.map((c) => ({
    value: `usr:${c.id}`,
    title: c.name,
    children: c.children?.length ? userNodes(c.children) : undefined,
  }))
}

function toValue(selection?: CategorySelection): string | undefined {
  if (!selection) return undefined
  if (selection.systemCategoryId) return `sys:${selection.systemCategoryId}`
  if (selection.userCategoryId) return `usr:${selection.userCategoryId}`
  return undefined
}

function fromValue(raw?: string): CategorySelection {
  if (!raw) return { systemCategoryId: null, userCategoryId: null }
  const [prefix, id] = raw.split(':')
  return {
    systemCategoryId: prefix === 'sys' ? id : null,
    userCategoryId: prefix === 'usr' ? id : null,
  }
}

/** Category selector grouping system and user categories, filtered by nature. */
export function CategorySelect({ nature, value, onChange, disabled }: CategorySelectProps) {
  const { t } = useTranslation()
  const { data: system } = useSystemCategories({ nature })
  const { data: user } = useUserCategories()

  const treeData: TreeNode[] = useMemo(() => {
    const userFiltered = (user ?? []).filter((c) => !nature || c.nature === nature)
    return [
      { value: 'group:system', title: t('finances.categories.systemGroup'), children: systemNodes(system ?? []) },
      { value: 'group:user', title: t('finances.categories.userGroup'), children: userNodes(userFiltered) },
    ]
  }, [system, user, nature, t])

  return (
    <TreeSelect
      style={{ width: '100%' }}
      disabled={disabled}
      allowClear
      showSearch
      treeNodeFilterProp="title"
      placeholder={t('finances.categories.selectPlaceholder')}
      value={toValue(value)}
      treeData={treeData}
      // "group:*" nodes are labels only and must not be selectable.
      onChange={(raw?: string) => {
        if (raw?.startsWith('group:')) return
        onChange?.(fromValue(raw))
      }}
    />
  )
}
