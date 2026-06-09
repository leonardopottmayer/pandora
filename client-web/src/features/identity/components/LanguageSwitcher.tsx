import { Segmented } from 'antd'
import { usePreferences } from '../context/preferences-context'
import type { AppLanguage } from '../models'

export function LanguageSwitcher({ size = 'small' }: { size?: 'small' | 'middle' | 'large' }) {
  const { language, setLanguage } = usePreferences()
  return (
    <Segmented<AppLanguage>
      size={size}
      value={language}
      onChange={setLanguage}
      options={[
        { label: 'PT', value: 'pt-BR' },
        { label: 'EN', value: 'en' },
      ]}
    />
  )
}
