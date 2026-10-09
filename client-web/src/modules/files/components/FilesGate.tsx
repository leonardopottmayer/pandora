import type { ReactNode } from 'react'
import { Button, Result, Spin } from 'antd'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useFilesPreferences } from '../hooks/useFiles'

/** Files screens show only once the account switch is on; until then they point to the settings. */
export function FilesGate({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const { data, isLoading } = useFilesPreferences()

  if (isLoading) return <Spin className="block py-16 text-center" />
  if (!data?.isEnabled)
    return (
      <Result
        title={t('files.gate.title')}
        subTitle={t('files.gate.desc')}
        extra={
          <Link to="/files/settings">
            <Button type="primary">{t('files.gate.action')}</Button>
          </Link>
        }
      />
    )
  return <>{children}</>
}
