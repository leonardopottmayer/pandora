import { useTranslation } from 'react-i18next'
import { Button, Dropdown, Tooltip, type MenuProps } from 'antd'
import { CheckOutlined, EllipsisOutlined } from '@ant-design/icons'
import { PAGE_FONTS, type PageAppearance, type PageDto } from '../models'

export type ExportKind = 'markdown' | 'html' | 'pdf'

interface PageMenuProps {
  page: PageDto
  onAppearance: (appearance: PageAppearance) => void
  onLocked: (locked: boolean) => void
  onTemplate: (isTemplate: boolean) => void
  /** Days it holds, null for no end, false to clear it. */
  onVerify: (days: number | null | false) => void
  onShare: () => void
  onExport: (kind: ExportKind) => void
  onSummarize?: () => void
}

const VERIFY_DAYS = [7, 30, 90, 365]

/** The page's own settings: look, lock, verification, public link and export. */
export function PageMenu({ page, onAppearance, onLocked, onTemplate, onVerify, onShare, onExport, onSummarize }: PageMenuProps) {
  const { t } = useTranslation()
  const look = page.appearance
  const check = (on: boolean) => (on ? <CheckOutlined /> : <span className="inline-block w-3.5" />)
  const set = (patch: Partial<PageAppearance>) => onAppearance({ ...look, ...patch })

  const items: MenuProps['items'] = [
    ...(onSummarize ? [{ key: 'summarize', label: t('notes.ai.summarizePage'), onClick: onSummarize }, { type: 'divider' as const }] : []),
    ...(!look.cover ? [{ key: 'cover', label: t('notes.cover.add'), onClick: () => set({ cover: { gradient: 'ocean', position: 50 } }) }] : []),
    { key: 'wide', icon: check(look.width === 'wide'), label: t('notes.pageMenu.wide'), onClick: () => set({ width: look.width === 'wide' ? 'normal' : 'wide' }) },
    { key: 'small', icon: check(look.smallText), label: t('notes.pageMenu.smallText'), onClick: () => set({ smallText: !look.smallText }) },
    {
      key: 'font',
      label: t('notes.pageMenu.font'),
      children: PAGE_FONTS.map((font) => ({ key: `font-${font}`, icon: check(look.font === font), label: t(`notes.pageMenu.fonts.${font}`), onClick: () => set({ font }) })),
    },
    { type: 'divider' },
    { key: 'lock', icon: check(page.isLocked), label: t('notes.pageMenu.lock'), onClick: () => onLocked(!page.isLocked) },
    { key: 'template', icon: check(page.isTemplate), label: t('notes.templates.mark'), onClick: () => onTemplate(!page.isTemplate) },
    {
      key: 'verify',
      label: t('notes.pageMenu.verify'),
      children: [
        ...VERIFY_DAYS.map((days) => ({ key: `verify-${days}`, label: t('notes.pageMenu.verifyFor', { count: days }), onClick: () => onVerify(days) })),
        { key: 'verify-forever', label: t('notes.pageMenu.verifyForever'), onClick: () => onVerify(null) },
        ...(page.verifiedAt ? [{ type: 'divider' as const }, { key: 'verify-clear', label: t('notes.pageMenu.verifyClear'), onClick: () => onVerify(false) }] : []),
      ],
    },
    { type: 'divider' },
    { key: 'share', label: t('notes.share.title'), onClick: onShare },
    {
      key: 'export',
      label: t('notes.pageMenu.export'),
      children: (['markdown', 'html', 'pdf'] as const).map((kind) => ({ key: `export-${kind}`, label: t(`notes.pageMenu.exports.${kind}`), onClick: () => onExport(kind) })),
    },
  ]

  return (
    <Dropdown trigger={['click']} placement="bottomRight" menu={{ items }}>
      <Tooltip title={t('notes.pageMenu.title')}>
        <Button type="text" icon={<EllipsisOutlined />} aria-label={t('notes.pageMenu.title')} />
      </Tooltip>
    </Dropdown>
  )
}
