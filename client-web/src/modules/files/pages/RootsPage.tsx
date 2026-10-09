import { useState } from 'react'
import { Alert, App, Button, Empty, Popconfirm, Progress, Space, Spin, Tag, Typography } from 'antd'
import { PlusOutlined, SyncOutlined } from '@ant-design/icons'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { PageHeading } from '@/components/settings/PageHeading'
import { SettingsSection } from '@/components/settings/SettingsSection'
import { SettingRow } from '@/components/settings/SettingRow'
import { toErrorMessage } from '@/lib/api/envelope'
import * as devicesService from '@/modules/identity/services/devices.service'
import type { Device } from '@/modules/identity/models'
import { FilesGate } from '../components/FilesGate'
import { RootFormModal } from '../components/RootFormModal'
import { SelectionDrawer } from '../components/SelectionDrawer'
import { filesKeys, useFilesAgent, useRoots } from '../hooks/useFiles'
import { shortTime } from '../lib/format'
import * as filesService from '../services/files.service'
import type { Root, Scan, ScanProgress } from '../models'

/** Devices and their roots: what each device catalogs, how its scans went, and the scans waiting for a decision. */
export function RootsPage() {
  return (
    <FilesGate>
      <Roots />
    </FilesGate>
  )
}

function Roots() {
  const { t } = useTranslation()
  const roots = useRoots()
  const devices = useQuery({ queryKey: ['identity', 'devices'], queryFn: devicesService.listDevices })
  const { agent, desktop, deviceId, status } = useFilesAgent()
  const [editing, setEditing] = useState<Root | 'new' | null>(null)
  const [selecting, setSelecting] = useState<Root | null>(null)

  if (roots.isLoading || devices.isLoading) return <Spin className="block py-16 text-center" />

  const allDevices = devices.data ?? []
  // Devices with roots, plus this PC when it scans; a root whose device was revoked keeps its own group.
  const groups = new Map<string, { device: Device | null; roots: Root[] }>()
  if (agent && deviceId) groups.set(deviceId, { device: allDevices.find((d) => d.id === deviceId) ?? null, roots: [] })
  for (const root of roots.data ?? []) {
    const group = groups.get(root.deviceId) ?? { device: allDevices.find((d) => d.id === root.deviceId) ?? null, roots: [] }
    group.roots.push(root)
    groups.set(root.deviceId, group)
  }

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <PageHeading title={t('files.roots.title')} description={t('files.roots.intro')} />
        <Button type="primary" icon={<PlusOutlined />} disabled={allDevices.length === 0} onClick={() => setEditing('new')}>
          {t('files.roots.add')}
        </Button>
      </div>

      {allDevices.length === 0 && (
        <Alert
          type="info"
          showIcon
          title={t('files.roots.noDevices')}
          description={<Link to="/account/devices">{t('files.roots.noDevicesAction')}</Link>}
        />
      )}
      {desktop && !agent && (
        <Alert type="info" showIcon title={t('files.roots.agentOff')} description={<Link to="/files/settings">{t('files.roots.agentOffAction')}</Link>} />
      )}
      {status?.paired === false && (
        <Alert type="warning" showIcon title={t('files.settings.notPaired')} description={<Link to="/account/devices">{t('files.settings.pairAction')}</Link>} />
      )}
      {status?.paired && status.lastError && <Alert type="warning" showIcon title={t('files.roots.agentError')} description={status.lastError} />}

      {groups.size === 0 && allDevices.length > 0 && (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('files.roots.empty')} />
      )}

      {[...groups].map(([id, group]) => (
        <SettingsSection
          key={id}
          title={
            <span>
              {group.device?.name ?? t('files.roots.unknownDevice')}{' '}
              {id === deviceId && <Tag color="purple">{t('devices.thisComputer')}</Tag>}
            </span>
          }
        >
          {group.roots.length === 0 ? (
            <Typography.Text type="secondary" className="block p-4">
              {t('files.roots.deviceEmpty')}
            </Typography.Text>
          ) : (
            group.roots.map((root) => (
              <RootRow key={root.id} root={root} onEdit={() => setEditing(root)} onSelect={() => setSelecting(root)} />
            ))
          )}
        </SettingsSection>
      ))}

      {editing && (
        <RootFormModal
          open
          root={editing === 'new' ? null : editing}
          devices={allDevices}
          onClose={() => setEditing(null)}
        />
      )}
      {selecting && <SelectionDrawer root={selecting} onClose={() => setSelecting(null)} />}
    </div>
  )
}

function RootRow({ root, onEdit, onSelect }: { root: Root; onEdit: () => void; onSelect: () => void }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const { agent, isHere, running } = useFilesAgent()
  const scans = useQuery({ queryKey: filesKeys.scans(root.id), queryFn: () => filesService.listScans(root.id) })
  const latest = scans.data?.[0]
  const progress = running?.rootId === root.id ? running : null

  const scanNow = useMutation({
    mutationFn: () => agent!.invoke('files.scanNow', { rootId: root.id }),
    onSuccess: () => message.info(t('files.roots.scanQueued')),
    onError: (e) => message.error(toErrorMessage(e, t('files.roots.scanError'))),
  })
  const remove = useMutation({
    mutationFn: () => filesService.removeRoot(root.id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: filesKeys.all }),
    onError: (e) => message.error(toErrorMessage(e, t('files.roots.removeError'))),
  })

  const facts = [
    root.localPath,
    root.scanTime ? t('files.roots.dailyAt', { time: shortTime(root.scanTime) }) : t('files.roots.manualOnly'),
    t('files.roots.entries', { count: root.entryCount }),
    root.lastCompletedScanAt
      ? t('files.roots.lastScan', { when: new Date(root.lastCompletedScanAt).toLocaleString() })
      : t('files.roots.neverScanned'),
  ]

  return (
    <SettingRow
      vertical
      label={root.name}
      description={
        <div className="flex flex-col gap-2">
          <span>{facts.join(' · ')}</span>
          {progress ? <ScanRunning progress={progress} /> : latest && <LatestScan scan={latest} />}
        </div>
      }
      control={
        <Space wrap>
          {isHere(root) && (
            <Button icon={<SyncOutlined />} loading={scanNow.isPending} disabled={progress !== null} onClick={() => scanNow.mutate()}>
              {t('files.roots.scanNow')}
            </Button>
          )}
          <Button onClick={onSelect}>{t('files.roots.selection')}</Button>
          <Button onClick={onEdit}>{t('common.edit')}</Button>
          <Popconfirm
            title={t('files.roots.removeConfirm', { name: root.name })}
            description={t('files.roots.removeDesc')}
            okButtonProps={{ danger: true }}
            onConfirm={() => remove.mutate()}
          >
            <Button danger loading={remove.isPending}>
              {t('files.roots.remove')}
            </Button>
          </Popconfirm>
        </Space>
      }
    />
  )
}

function ScanRunning({ progress }: { progress: ScanProgress }) {
  const { t } = useTranslation()
  return (
    <div>
      <Typography.Text>
        {t('files.scan.progress', { walked: progress.walked, fingerprinted: progress.fingerprinted })}
      </Typography.Text>
      <Progress percent={100} status="active" showInfo={false} size="small" />
    </div>
  )
}

function LatestScan({ scan }: { scan: Scan }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const resolve = useMutation({
    mutationFn: (action: 'confirm' | 'discard') => filesService.resolveScan(scan.id, action),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: filesKeys.all }),
    onError: (e) => message.error(toErrorMessage(e, t('files.scan.resolveError'))),
  })
  const counts = t('files.scan.counts', {
    when: new Date(scan.finishedAt ?? scan.startedAt).toLocaleString(),
    seen: scan.seen,
    created: scan.created,
    changed: scan.changed,
    moved: scan.moved,
    missing: scan.missing,
    excluded: scan.excluded,
  })

  if (scan.status === 'held')
    return (
      <Alert
        type="warning"
        showIcon
        title={t('files.scan.held', { missing: scan.missing, seen: scan.seen })}
        description={
          <div className="flex flex-col gap-2">
            <span>{t('files.scan.heldDesc')}</span>
            <Space>
              <Button size="small" type="primary" loading={resolve.isPending} onClick={() => resolve.mutate('confirm')}>
                {t('files.scan.confirm')}
              </Button>
              <Button size="small" loading={resolve.isPending} onClick={() => resolve.mutate('discard')}>
                {t('files.scan.discard')}
              </Button>
            </Space>
          </div>
        }
      />
    )
  if (scan.status === 'aborted')
    return <Typography.Text type="danger">{t('files.scan.aborted', { error: t(`files.scan.reason.${scan.error}`, { defaultValue: scan.error ?? '—' }) })}</Typography.Text>
  if (scan.status === 'running')
    return <Typography.Text type="secondary">{t('files.scan.runningElsewhere', { when: new Date(scan.startedAt).toLocaleString() })}</Typography.Text>
  return <Typography.Text type="secondary">{counts}</Typography.Text>
}
