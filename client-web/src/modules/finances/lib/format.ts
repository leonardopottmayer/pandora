import i18n from '@/i18n'

/** Maps the app language to a BCP-47 locale for Intl. */
function currentLocale(): string {
  return i18n.language === 'en' ? 'en-US' : 'pt-BR'
}

/**
 * Formats a monetary amount using Intl.NumberFormat with the backend currency
 * (ISO 4217 or crypto ticker). Crypto tickers unknown to Intl fall back to
 * decimal + code format (e.g. "0.50000000 BTC").
 */
export function formatMoney(amount: number, currency: string): string {
  const locale = currentLocale()
  try {
    return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount)
  } catch {
    const digits = new Intl.NumberFormat(locale, {
      minimumFractionDigits: 2,
      maximumFractionDigits: 8,
    }).format(amount)
    return `${digits} ${currency}`
  }
}

/** Formats a `DateOnly` date ("yyyy-MM-dd") in the current locale without timezone drift. */
export function formatDate(isoDate: string | null | undefined): string {
  if (!isoDate) return ''
  const [year, month, day] = isoDate.split('-').map(Number)
  if (!year || !month || !day) return isoDate
  return new Intl.DateTimeFormat(currentLocale()).format(new Date(year, month - 1, day))
}

/** Formats a `DateTimeOffset` (ISO 8601) as date+time in the current locale. */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return new Intl.DateTimeFormat(currentLocale(), { dateStyle: 'short', timeStyle: 'short' }).format(d)
}

/** Formats a statement reference month ("yyyy-MM" or "yyyy-MM-dd") as month/year. */
export function formatReferenceMonth(reference: string | null | undefined): string {
  if (!reference) return ''
  const [year, month] = reference.split('-').map(Number)
  if (!year || !month) return reference
  return new Intl.DateTimeFormat(currentLocale(), { month: 'short', year: 'numeric' }).format(
    new Date(year, month - 1, 1),
  )
}
