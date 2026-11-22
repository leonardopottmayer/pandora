import { useTranslation } from 'react-i18next'
import { App, Skeleton, Switch, Tag } from 'antd'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { SettingRow } from '@/components/settings/SettingRow'
import { toErrorMessage } from '@/lib/api/envelope'
import type { FeatureDto } from '../../models'
import { useFeatures, useSetFeature } from '../../hooks/useAdmin'
import { featureLabel } from '../../lib/labels'
import { AdminError } from '../../components/AdminError'

/** The flags as a tree: a child sits under its parent and is off whenever the parent is. */
export function FeaturesPage() {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const { data, isLoading, error } = useFeatures()
  const setFeature = useSetFeature()

  if (error) return <AdminError error={error} />

  const features = data ?? []
  const byKey = new Map(features.map((f) => [f.key, f]))
  const childrenOf = (key: string) => features.filter((f) => f.parent === key)
  const roots = features.filter((f) => !f.parent)

  async function save(feature: FeatureDto, enabled: boolean) {
    try {
      await setFeature.mutateAsync({ key: feature.key, enabled })
      message.success(enabled ? t('admin.features.turnedOn') : t('admin.features.turnedOff'))
    } catch (err) {
      message.error(toErrorMessage(err, t('admin.saveError')))
    }
  }

  // Turning a flag off hides it from everyone, the administrators included: say so first.
  function toggle(feature: FeatureDto, enabled: boolean) {
    if (enabled) {
      void save(feature, true)
      return
    }
    const children = childrenOf(feature.key).map((c) => featureLabel(t, c.key))
    modal.confirm({
      title: t('admin.features.turnOffConfirm', { name: featureLabel(t, feature.key) }),
      content: children.length
        ? t('admin.features.turnOffWithChildren', { children: children.join(', ') })
        : t('admin.features.turnOffContent'),
      okText: t('admin.features.turnOff'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () => save(feature, false),
    })
  }

  // Depth-first, so each child follows its parent; flat, so every row keeps its divider.
  const ordered: { feature: FeatureDto; depth: number }[] = []
  const visit = (feature: FeatureDto, depth: number) => {
    ordered.push({ feature, depth })
    childrenOf(feature.key).forEach((child) => visit(child, depth + 1))
  }
  roots.forEach((f) => visit(f, 0))

  function row({ feature, depth }: { feature: FeatureDto; depth: number }) {
    const parentOff = !!feature.parent && !byKey.get(feature.parent)?.enabled
    const on = feature.override ?? feature.defaultEnabled
    return (
      <SettingRow
        key={feature.key}
        label={
          <span className="flex flex-wrap items-center gap-2" style={{ paddingLeft: depth * 24 }}>
            {featureLabel(t, feature.key)}
            {feature.override !== null && feature.override !== feature.defaultEnabled && (
              <Tag color="gold">{t('admin.features.changed')}</Tag>
            )}
          </span>
        }
        description={
          <span style={{ paddingLeft: depth * 24 }}>
            {parentOff ? t('admin.features.offWithParent') : <span className="font-mono">{feature.key}</span>}
          </span>
        }
        control={
          <Switch
            checked={on}
            disabled={parentOff}
            loading={setFeature.isPending && setFeature.variables?.key === feature.key}
            onChange={(checked) => toggle(feature, checked)}
            aria-label={featureLabel(t, feature.key)}
          />
        }
      />
    )
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <PageHeading title={t('nav.adminFeatures')} description={t('admin.features.subtitle')} />
      <SettingsSection>{isLoading ? <Skeleton active className="p-4" /> : ordered.map(row)}</SettingsSection>
    </div>
  )
}
