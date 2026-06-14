import i18n from '@/i18n'

/** Mapeia o idioma do app para um locale BCP-47 usado por Intl. */
function currentLocale(): string {
  return i18n.language === 'en' ? 'en-US' : 'pt-BR'
}

/**
 * Formata um valor monetário. Usa Intl.NumberFormat com a moeda do backend
 * (ISO 4217 ou ticker de cripto). Tickers de cripto não reconhecidos pelo
 * Intl caem num formato decimal + código (ex.: "0,50000000 BTC").
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

/** Formata uma data `DateOnly` ("yyyy-MM-dd") no locale atual, sem deslocamento de fuso. */
export function formatDate(isoDate: string | null | undefined): string {
  if (!isoDate) return ''
  const [year, month, day] = isoDate.split('-').map(Number)
  if (!year || !month || !day) return isoDate
  return new Intl.DateTimeFormat(currentLocale()).format(new Date(year, month - 1, day))
}

/** Formata um `DateTimeOffset` (ISO 8601) como data+hora no locale atual. */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return new Intl.DateTimeFormat(currentLocale(), { dateStyle: 'short', timeStyle: 'short' }).format(d)
}

/** Formata o mês de referência de fatura ("yyyy-MM" ou "yyyy-MM-dd") como mês/ano. */
export function formatReferenceMonth(reference: string | null | undefined): string {
  if (!reference) return ''
  const [year, month] = reference.split('-').map(Number)
  if (!year || !month) return reference
  return new Intl.DateTimeFormat(currentLocale(), { month: 'short', year: 'numeric' }).format(
    new Date(year, month - 1, 1),
  )
}
