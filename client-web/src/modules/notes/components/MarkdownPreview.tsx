import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { App } from 'antd'
import { Marked } from 'marked'
import DOMPurify from 'dompurify'
import { downloadAttachment } from '../services/attachments.service'
import { useAttachmentUrls } from '../hooks/useAttachmentUrls'
import { attachmentFileName, isAttachmentUrl, withAttachmentUrls } from '../lib/attachments'
import { calloutExtension, CALLOUT_TYPES, type CalloutLabels } from '../lib/callouts'
import { codeRenderer } from '../lib/codeBlock'
import { renderTags, type TagIndex } from '../lib/tags'
import { renderWikilinks, type PageIndex } from '../lib/wikilinks'

interface MarkdownPreviewProps {
  markdown: string
  /** Resolves `[[wikilinks]]`; without it they render as plain text. */
  pageIndex?: PageIndex
  /** Paints `#tags` with the color each one carries; without it they render uncolored. */
  tagIndex?: TagIndex
  /** A wikilink pointing at an existing page was clicked. */
  onOpenPage?: (pageId: string) => void
  /** A wikilink pointing at nothing was clicked — the "create on click" flow. */
  onCreatePage?: (title: string) => void
  /** A `#tag` chip was clicked, by slug — the caller turns it into a filter. */
  onSelectTag?: (slug: string) => void
}

function renderHtml(
  parser: Marked,
  md: string,
  attachmentUrls: ReadonlyMap<string, string>,
  pageIndex?: PageIndex,
  tagIndex?: TagIndex,
): string {
  const withLinks = pageIndex ? renderWikilinks(md, pageIndex) : md
  // Tags after the wikilinks: neither pass can produce the other's syntax, and both run before the
  // markdown lexer so what they emit is HTML the parser passes through.
  const withTags = renderTags(withLinks, tagIndex ?? new Map())
  const raw = parser.parse(withTags, { async: false })

  // The object URLs go in after the sanitizer, and they are part of the rendered HTML: an image
  // whose src is only set on the DOM node is lost on the next re-render.
  return withAttachmentUrls(DOMPurify.sanitize(raw), attachmentUrls)
}

/**
 * Renders markdown to HTML. Embedded attachments point at an authenticated endpoint that the browser
 * cannot reach on its own (the Bearer token lives in localStorage, not a cookie): images are fetched
 * through the API client and rendered as object URLs, and a click on an attachment link is turned
 * into a download rather than a navigation to a URL that would answer 401.
 */
export function MarkdownPreview({
  markdown: md,
  pageIndex,
  tagIndex,
  onOpenPage,
  onCreatePage,
  onSelectTag,
}: MarkdownPreviewProps) {
  const { t, i18n } = useTranslation()
  const { message } = App.useApp()
  const attachmentUrls = useAttachmentUrls(md)

  // A callout written without a title falls back to the name of its type, so the parser is rebuilt
  // when the language changes.
  const parser = useMemo(() => {
    const labels = Object.fromEntries(
      CALLOUT_TYPES.map((type) => [type, t(`notes.callout.${type}`)]),
    ) as CalloutLabels
    return new Marked({ extensions: [calloutExtension(labels)], renderer: codeRenderer })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [i18n.language])

  const html = useMemo(
    () => renderHtml(parser, md, attachmentUrls, pageIndex, tagIndex),
    [parser, md, attachmentUrls, pageIndex, tagIndex],
  )

  // Wikilinks are anchors with no real href; a delegated handler turns a click into navigation (or
  // into creating the page that is still missing).
  function handleClick(event: React.MouseEvent<HTMLDivElement>) {
    const tag = (event.target as HTMLElement).closest<HTMLElement>('.notes-tag')
    if (tag?.dataset.tagSlug) {
      event.preventDefault()
      onSelectTag?.(tag.dataset.tagSlug)
      return
    }

    const anchor = (event.target as HTMLElement).closest<HTMLAnchorElement>('a')
    if (!anchor) return

    // An attachment link: the href is an API path, so letting the browser follow it would replace
    // the tab with an unauthenticated request. Fetch it and save it instead.
    const href = anchor.getAttribute('href') ?? ''
    if (isAttachmentUrl(href)) {
      event.preventDefault()
      void downloadAttachment(href, attachmentFileName(href, anchor.textContent)).catch(() =>
        message.error(t('notes.downloadError')),
      )
      return
    }

    if (!anchor.classList.contains('notes-wikilink')) return

    event.preventDefault()
    const pageId = anchor.dataset.pageId
    if (pageId) onOpenPage?.(pageId)
    else if (anchor.dataset.pageTitle) onCreatePage?.(anchor.dataset.pageTitle)
  }

  return (
    <div
      className="notes-preview"
      onClick={handleClick}
      // Sanitized above with DOMPurify.
      dangerouslySetInnerHTML={{ __html: html }}
    />
  )
}
