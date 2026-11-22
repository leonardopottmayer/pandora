import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Empty, Spin, Typography, theme } from 'antd'
import { PageCover } from '../components/PageCover'
import { VisualEditor } from '../components/visual/VisualEditor'
import { noteKeys } from '../hooks/queryKeys'
import { setPublicMediaToken } from '../lib/media'
import { getPublicPage } from '../services/pages.service'
import '../components/notes.css'

/** A page opened by its public link: read-only, no sign-in, nothing of the rest of the notes. */
export function PublicPage() {
  const { t } = useTranslation()
  const { token: theme_ } = theme.useToken()
  const { token = '' } = useParams<{ token: string }>()
  // Set while rendering, before the blocks below ask for their images: they come through this link.
  setPublicMediaToken(token)
  useEffect(() => () => setPublicMediaToken(null), [])

  const { data: page, isLoading, isError } = useQuery({
    queryKey: noteKeys.publicPage(token),
    queryFn: () => getPublicPage(token),
    retry: false,
  })

  if (isLoading) return <div className="flex min-h-dvh items-center justify-center"><Spin /></div>
  if (isError || !page) {
    return (
      <div className="flex min-h-dvh items-center justify-center p-6">
        <Empty description={t('notes.public.unavailable')} />
      </div>
    )
  }

  const look = page.appearance
  return (
    <div className="notes-print min-h-dvh" style={{ background: theme_.colorBgContainer }}>
      <PageCover cover={look.cover} editable={false} onChange={() => undefined} />
      <main className={`mx-auto w-full px-5 py-10 md:px-10 ${look.width === 'wide' ? 'max-w-[1500px]' : 'max-w-[760px]'}`}>
        <div className={`notes-doc notes-font-${look.font} ${look.smallText ? 'notes-small-text' : ''}`}>
          {page.icon && <div className="mb-2 text-4xl leading-none">{page.icon}</div>}
          <Typography.Title level={1} className="!mb-6 !text-3xl !leading-tight">{page.title || t('notes.untitled')}</Typography.Title>
          <VisualEditor docId={`public-${token}`} initialContent={page.content.blocks} pages={[]} editable={false} publicView onOpenPage={() => undefined} />
        </div>
        <Typography.Text type="secondary" className="mt-12 block text-center text-xs">
          {t('notes.public.footer')}
        </Typography.Text>
      </main>
    </div>
  )
}
