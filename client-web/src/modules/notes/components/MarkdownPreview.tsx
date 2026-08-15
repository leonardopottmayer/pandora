import { useEffect, useMemo, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Marked } from 'marked'
import DOMPurify from 'dompurify'
import { fetchAttachmentBlob } from '../services/attachments.service'
import { calloutExtension, CALLOUT_TYPES, type CalloutLabels } from '../lib/callouts'
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

const ATTACHMENT_PREFIX = '/api/v1/notes/attachments/'

function renderHtml(parser: Marked, md: string, pageIndex?: PageIndex, tagIndex?: TagIndex): string {
  const withLinks = pageIndex ? renderWikilinks(md, pageIndex) : md
  // Tags after the wikilinks: neither pass can produce the other's syntax, and both run before the
  // markdown lexer so what they emit is HTML the parser passes through.
  const withTags = renderTags(withLinks, tagIndex ?? new Map())
  const raw = parser.parse(withTags, { async: false })
  return DOMPurify.sanitize(raw)
}

/**
 * Renders markdown to HTML. Embedded attachment images point at an authenticated endpoint that a
 * plain `<img src>` can't reach (the Bearer token lives in localStorage, not a cookie), so after
 * each render we fetch those blobs through the API client and swap in object URLs.
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
  const containerRef = useRef<HTMLDivElement>(null)

  // A callout written without a title falls back to the name of its type, so the parser is rebuilt
  // when the language changes.
  const parser = useMemo(() => {
    const labels = Object.fromEntries(
      CALLOUT_TYPES.map((type) => [type, t(`notes.callout.${type}`)]),
    ) as CalloutLabels
    return new Marked({ extensions: [calloutExtension(labels)] })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [i18n.language])

  const html = useMemo(
    () => renderHtml(parser, md, pageIndex, tagIndex),
    [parser, md, pageIndex, tagIndex],
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

    const anchor = (event.target as HTMLElement).closest<HTMLAnchorElement>('a.notes-wikilink')
    if (!anchor) return

    event.preventDefault()
    const pageId = anchor.dataset.pageId
    if (pageId) onOpenPage?.(pageId)
    else if (anchor.dataset.pageTitle) onCreatePage?.(anchor.dataset.pageTitle)
  }

  useEffect(() => {
    const container = containerRef.current
    if (!container) return

    const objectUrls: string[] = []
    let cancelled = false

    const images = container.querySelectorAll<HTMLImageElement>('img')
    images.forEach((img) => {
      // getAttribute keeps the original path; img.src would already be absolute.
      const src = img.getAttribute('src') ?? ''
      if (!src.includes(ATTACHMENT_PREFIX)) return
      const path = src.slice(src.indexOf(ATTACHMENT_PREFIX))
      void fetchAttachmentBlob(path)
        .then((blob) => {
          if (cancelled) return
          const objectUrl = URL.createObjectURL(blob)
          objectUrls.push(objectUrl)
          img.src = objectUrl
        })
        .catch(() => {
          /* leave the broken image; the source path stays visible for debugging */
        })
    })

    return () => {
      cancelled = true
      for (const url of objectUrls) URL.revokeObjectURL(url)
    }
  }, [html])

  return (
    <div
      ref={containerRef}
      className="notes-preview"
      onClick={handleClick}
      // Sanitized above with DOMPurify.
      dangerouslySetInnerHTML={{ __html: html }}
    />
  )
}
