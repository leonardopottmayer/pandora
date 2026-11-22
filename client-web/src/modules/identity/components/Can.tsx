import type { ReactNode } from 'react'
import { useAllows } from '../hooks/useAccess'

interface CanProps {
  permission?: string
  feature?: string
  children: ReactNode
  /** Shown instead when the user may not. */
  fallback?: ReactNode
}

/** Renders its children only when the feature is on and the user holds the permission. */
export function Can({ permission, feature, children, fallback = null }: CanProps) {
  return useAllows({ permission, feature }) ? <>{children}</> : <>{fallback}</>
}
