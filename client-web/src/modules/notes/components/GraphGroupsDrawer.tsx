import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { App, Button, ColorPicker, Divider, Drawer, Empty, Flex, Input, Select, Typography, theme } from 'antd'
import { ArrowDownOutlined, ArrowUpOutlined, DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import type { GraphFamily } from '../lib/graphGroups'
import type { TagDto } from '../models'

interface GraphGroupsDrawerProps {
  families: GraphFamily[]
  tags: TagDto[]
  onSave: (families: GraphFamily[]) => void
  onClose: () => void
}

export function GraphGroupsDrawer({ families, tags, onSave, onClose }: GraphGroupsDrawerProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const { message } = App.useApp()
  const [draft, setDraft] = useState(() => families.map((family) => ({ ...family, tagIds: [...family.tagIds] })))

  function change(id: string, patch: Partial<GraphFamily>) {
    setDraft((current) => current.map((family) => family.id === id ? { ...family, ...patch } : family))
  }

  function move(index: number, delta: number) {
    setDraft((current) => {
      const next = [...current]
      ;[next[index], next[index + delta]] = [next[index + delta], next[index]]
      return next
    })
  }

  function save() {
    if (draft.some((family) => !family.name.trim() || family.tagIds.length === 0)) {
      void message.warning(t('notes.graphGroups.required'))
      return
    }
    onSave(draft.map((family) => ({ ...family, name: family.name.trim() })))
    onClose()
  }

  return (
    <Drawer
      open
      title={t('notes.graphGroups.configure')}
      onClose={onClose}
      size={440}
      styles={{ wrapper: { maxWidth: '100vw' } }}
      footer={<Flex justify="end" gap={8}>
        <Button onClick={onClose}>{t('common.cancel')}</Button>
        <Button type="primary" onClick={save}>{t('common.save')}</Button>
      </Flex>}
    >
      <Typography.Paragraph type="secondary">{t('notes.graphGroups.hint')}</Typography.Paragraph>
      <Typography.Paragraph type="secondary">{t('notes.graphGroups.device')}</Typography.Paragraph>
      {draft.length === 0 && <Empty description={t('notes.graphGroups.empty')} image={Empty.PRESENTED_IMAGE_SIMPLE} />}
      {draft.map((family, index) => (
        <div key={family.id}>
          <Flex gap={8} align="center" style={{ marginBottom: 8 }}>
            <ColorPicker
              value={family.color}
              disabledAlpha
              onChangeComplete={(color) => change(family.id, { color: color.toHexString() })}
            >
              <Button aria-label={t('notes.graphGroups.color', { name: family.name })} style={{ color: family.color }}>●</Button>
            </ColorPicker>
            <Input
              value={family.name}
              maxLength={80}
              placeholder={t('notes.graphGroups.name')}
              aria-label={t('notes.graphGroups.name')}
              onChange={(event) => change(family.id, { name: event.target.value })}
            />
          </Flex>
          <Select
            mode="multiple"
            value={family.tagIds}
            style={{ width: '100%' }}
            placeholder={t('notes.graphGroups.tags')}
            aria-label={t('notes.graphGroups.tags')}
            showSearch={{ optionFilterProp: 'label' }}
            options={[
              ...tags.map((tag) => ({ value: tag.id, label: `#${tag.name}` })),
              ...family.tagIds.filter((id) => !tags.some((tag) => tag.id === id))
                .map((id) => ({ value: id, label: t('notes.graphGroups.missingTag'), disabled: true })),
            ]}
            onChange={(tagIds) => change(family.id, { tagIds })}
          />
          <Flex gap={4} justify="end" style={{ marginTop: 8 }}>
            <Button icon={<ArrowUpOutlined />} disabled={index === 0} aria-label={t('notes.graphGroups.up')} onClick={() => move(index, -1)} />
            <Button icon={<ArrowDownOutlined />} disabled={index === draft.length - 1} aria-label={t('notes.graphGroups.down')} onClick={() => move(index, 1)} />
            <Button icon={<DeleteOutlined />} aria-label={t('notes.graphGroups.remove')} onClick={() => setDraft(draft.filter((item) => item.id !== family.id))} />
          </Flex>
          <Divider />
        </div>
      ))}
      <Button
        icon={<PlusOutlined />}
        block
        onClick={() => setDraft([...draft, { id: crypto.randomUUID(), name: '', tagIds: [], color: token.colorPrimary }])}
      >{t('notes.graphGroups.add')}</Button>
    </Drawer>
  )
}
