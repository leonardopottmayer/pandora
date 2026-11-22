import type { ReactNode } from 'react'
import { Typography } from 'antd'

/** Standard page title + optional description used across the settings screens. */
export function PageHeading({ title, description }: { title: ReactNode; description?: ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <Typography.Title level={4} className="!mb-0">
        {title}
      </Typography.Title>
      {description && (
        <Typography.Text type="secondary" className="max-w-prose">
          {description}
        </Typography.Text>
      )}
    </div>
  )
}
