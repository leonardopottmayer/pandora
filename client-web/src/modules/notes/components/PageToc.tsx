import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Typography, theme } from 'antd'
import { headingsOf, type Block } from '../lib/blocks'

interface PageTocProps {
  blocks: Block[]
  /** The element the page scrolls in, to tell which section is being read. */
  scroller: HTMLElement | null
  /** Beside the page, sticky; or above it, folded, when there is no room beside. */
  placement: 'side' | 'top'
}

/** "On this page": the page's headings, a click away, the one being read marked. */
export function PageToc({ blocks, scroller, placement }: PageTocProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const headings = useMemo(() => headingsOf(blocks), [blocks])
  const [active, setActive] = useState<string | null>(null)

  useEffect(() => {
    if (!scroller || headings.length === 0) return
    const track = () => {
      const top = scroller.getBoundingClientRect().top + 96
      let current: string | null = headings[0]?.id ?? null
      for (const heading of headings) {
        const element = scroller.querySelector(`[data-block-id="${heading.id}"]`)
        if (element && element.getBoundingClientRect().top <= top) current = heading.id
      }
      setActive(current)
    }
    track()
    scroller.addEventListener('scroll', track, { passive: true })
    return () => scroller.removeEventListener('scroll', track)
  }, [scroller, headings])

  if (headings.length === 0) return null

  const go = (id: string) => {
    const reduce = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
    scroller?.querySelector(`[data-block-id="${id}"]`)?.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'start' })
  }

  const list = (
    <ul className="m-0 flex list-none flex-col gap-px p-0" style={placement === 'side' ? { borderLeft: `1px solid ${token.colorBorderSecondary}` } : undefined}>
      {headings.map((heading) => (
        <li key={heading.id}>
          <button
            type="button"
            title={heading.text}
            className="block w-full cursor-pointer truncate rounded-r py-0.5 pe-2 text-left text-[13px] hover:bg-[var(--ant-color-fill-tertiary)]"
            style={{
              border: 0,
              borderLeft: placement === 'side' ? `2px solid ${active === heading.id ? token.colorPrimary : 'transparent'}` : 0,
              marginLeft: placement === 'side' ? -1 : 0,
              paddingInlineStart: 10 + (heading.level - 1) * 12,
              background: 'transparent',
              color: active === heading.id ? token.colorLink : token.colorTextSecondary,
            }}
            onClick={() => go(heading.id)}
          >
            {heading.text}
          </button>
        </li>
      ))}
    </ul>
  )

  const title = (
    <Typography.Text type="secondary" className="text-xs font-semibold uppercase tracking-wider">
      {t('notes.toc.title')}
    </Typography.Text>
  )

  return placement === 'side' ? (
    <nav aria-label={t('notes.toc.title')} className="sticky top-8 flex flex-col gap-2">
      <span className="px-2.5">{title}</span>
      {list}
    </nav>
  ) : (
    <details open className="mb-6 rounded-xl px-3 py-2" style={{ border: `1px solid ${token.colorBorderSecondary}`, background: token.colorFillQuaternary }}>
      <summary className="cursor-pointer select-none">{title}</summary>
      <nav aria-label={t('notes.toc.title')} className="pt-2">{list}</nav>
    </details>
  )
}
