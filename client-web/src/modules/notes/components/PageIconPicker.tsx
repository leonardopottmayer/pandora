import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Flex, Input, Popover, Tooltip } from 'antd'
import { FileTextOutlined } from '@ant-design/icons'
import { normalizeIcon, SUGGESTED_ICONS } from '../lib/pageIcon'

interface PageIconPickerProps {
  value: string | null
  onChange: (icon: string | null) => void
}

/**
 * Sets the emoji shown next to a page in the sidebar. It is an input plus a few starters rather than
 * a full emoji library: every OS already ships a searchable emoji panel (Win+. on Windows), and
 * anything typed is reduced to its first grapheme, so the field cannot become a second title.
 */
export function PageIconPicker({ value, onChange }: PageIconPickerProps) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)

  function choose(icon: string | null) {
    onChange(icon)
    setOpen(false)
  }

  const content = (
    <Flex vertical gap={8} style={{ width: 232 }}>
      <Input
        autoFocus
        allowClear
        size="small"
        placeholder={t('notes.iconPlaceholder')}
        // Controlled by the page, not by the field: what is typed is normalized on the way out.
        value={value ?? ''}
        onChange={(e) => onChange(normalizeIcon(e.target.value))}
        onPressEnter={() => setOpen(false)}
      />
      <Flex wrap gap={2}>
        {SUGGESTED_ICONS.map((icon) => (
          <Button key={icon} type="text" size="small" onClick={() => choose(icon)}>
            {icon}
          </Button>
        ))}
      </Flex>
      <Button size="small" block disabled={value === null} onClick={() => choose(null)}>
        {t('notes.iconRemove')}
      </Button>
    </Flex>
  )

  return (
    <Popover
      open={open}
      onOpenChange={setOpen}
      trigger="click"
      placement="bottomLeft"
      title={t('notes.icon')}
      content={content}
    >
      <Tooltip title={t('notes.icon')}>
        <Button type="text" style={{ fontSize: 20, flexShrink: 0, padding: '0 6px' }}>
          {value ?? <FileTextOutlined style={{ opacity: 0.45 }} />}
        </Button>
      </Tooltip>
    </Popover>
  )
}
