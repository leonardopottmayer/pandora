import { describe, it, expect } from 'vitest'
import { Marked } from 'marked'
import DOMPurify from 'dompurify'
import { calloutExtension, CALLOUT_TYPES, type CalloutLabels } from './callouts'

const labels = Object.fromEntries(
  CALLOUT_TYPES.map((type) => [type, `Label ${type}`]),
) as CalloutLabels

const parser = new Marked({ extensions: [calloutExtension(labels)] })

function render(markdown: string): string {
  return parser.parse(markdown, { async: false })
}

describe('calloutExtension', () => {
  it.each(CALLOUT_TYPES)('renders a %s callout with its own class', (type) => {
    expect(render(`> [!${type}] Heads up`)).toContain(`notes-callout notes-callout-${type}`)
  })

  it('uses the title written after the marker', () => {
    expect(render('> [!warning] Careful now')).toContain('Careful now')
  })

  it('falls back to the localized label when no title was written', () => {
    const html = render('> [!tip]')
    expect(html).toContain('Label tip')
  })

  it('parses the body as markdown', () => {
    const html = render('> [!note] Title\n> Some **bold** text')
    expect(html).toContain('<strong>bold</strong>')
    expect(html).toContain('notes-callout-body')
  })

  it('keeps a callout without a body free of an empty body div', () => {
    expect(render('> [!note] Just a title')).not.toContain('notes-callout-body')
  })

  it('leaves an unknown type as a plain blockquote', () => {
    const html = render('> [!frobnicate] Nope')
    expect(html).toContain('<blockquote>')
    expect(html).not.toContain('notes-callout')
  })

  it('leaves an ordinary blockquote alone', () => {
    expect(render('> just a quote')).toContain('<blockquote>')
  })

  it('escapes html in the title', () => {
    expect(render('> [!note] <script>x</script>')).not.toContain('<script>')
  })

  it('stops at the end of the blockquote', () => {
    const html = render('> [!info] Inside\n\nOutside paragraph')
    expect(html).toContain('<p>Outside paragraph</p>')
    expect(html.indexOf('Outside paragraph')).toBeGreaterThan(html.indexOf('</div>'))
  })

  it('matches the type case-insensitively', () => {
    expect(render('> [!WARNING] Shout')).toContain('notes-callout-warning')
  })

  it('keeps its classes through the sanitizer the preview runs', () => {
    const html = DOMPurify.sanitize(render('> [!danger] Boom\n> body'))
    expect(html).toContain('notes-callout notes-callout-danger')
    expect(html).toContain('notes-callout-body')
  })
})
