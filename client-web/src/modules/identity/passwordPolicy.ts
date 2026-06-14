import i18n from '@/i18n'

export function isStrongPassword(value: string): boolean {
  if (!value || value.length < 8) return false
  const hasUpper = /[A-Z]/.test(value)
  const hasDigit = /[0-9]/.test(value)
  const hasSpecial = /[^A-Za-z0-9\s]/.test(value)
  return hasUpper && hasDigit && hasSpecial
}

/** Reusable antd rule for new-password fields (messages via i18n). */
export const passwordRule = {
  validator(_rule: unknown, value: string) {
    if (!value) return Promise.reject(new Error(i18n.t('password.required')))
    if (!isStrongPassword(value)) return Promise.reject(new Error(i18n.t('password.hint')))
    return Promise.resolve()
  },
}
