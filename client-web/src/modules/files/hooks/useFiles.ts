import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { desktopSettingsKey, useDesktop, type DesktopSettings } from '@/lib/desktop'
import * as filesService from '../services/files.service'
import type { AgentStatus, Root, ScanProgress } from '../models'

// Central query keys of the Files module; invalidating `all` refreshes every Files screen.
export const filesKeys = {
  all: ['files'] as const,
  preferences: () => [...filesKeys.all, 'preferences'] as const,
  roots: () => [...filesKeys.all, 'roots'] as const,
  scans: (rootId: string) => [...filesKeys.all, 'scans', rootId] as const,
  filters: () => [...filesKeys.all, 'filters'] as const,
  catalog: () => [...filesKeys.all, 'catalog'] as const,
  review: () => [...filesKeys.all, 'review'] as const,
  agent: () => [...filesKeys.all, 'agent'] as const,
}

export function useFilesPreferences() {
  return useQuery({ queryKey: filesKeys.preferences(), queryFn: filesService.getPreferences })
}

export function useRoots() {
  return useQuery({ queryKey: filesKeys.roots(), queryFn: filesService.listRoots })
}

export function useFilters() {
  return useQuery({ queryKey: filesKeys.filters(), queryFn: filesService.listFilters })
}

/**
 * This computer's Files agent, when the page runs inside Pandora Desktop with the Files module on.
 * `agent` is null everywhere else, and every desktop-only control hides on it.
 */
export function useFilesAgent() {
  const desktop = useDesktop()
  const queryClient = useQueryClient()

  const capabilities = useQuery({
    queryKey: ['desktop', 'capabilities'],
    queryFn: () => desktop!.capabilities(),
    enabled: desktop !== null,
  })
  const settings = useQuery({
    queryKey: desktopSettingsKey,
    queryFn: () => desktop!.invoke<DesktopSettings>('desktop.getSettings'),
    enabled: desktop !== null,
  })
  const agent = desktop && capabilities.data?.includes('files') ? desktop : null

  // files.status also makes the agent fetch its configuration again, so roots added on this page are known to it.
  const status = useQuery({
    queryKey: filesKeys.agent(),
    queryFn: () => agent!.invoke<AgentStatus>('files.status'),
    enabled: agent !== null,
  })

  const [progress, setProgress] = useState<ScanProgress | null | undefined>(undefined)
  useEffect(() => {
    if (!agent) return
    return agent.on('files.scanProgress', (payload) => {
      const p = payload as ScanProgress
      setProgress(p.state === 'running' ? p : null)
      if (p.state !== 'running') void queryClient.invalidateQueries({ queryKey: filesKeys.all })
    })
  }, [agent, queryClient])

  const deviceId = settings.data?.deviceId ?? null
  return {
    /** The bridge, only when this PC runs the Files agent. */
    agent,
    /** Inside Pandora Desktop (whether or not the Files module is on). */
    desktop,
    /** The device this PC is paired as. */
    deviceId,
    status: status.data ?? null,
    running: progress === undefined ? (status.data?.running ?? null) : progress,
    /** Whether the root lives on this PC, so the agent can scan it, list its folders and reveal its files. */
    isHere: (root: Root) => agent !== null && root.deviceId === deviceId,
  }
}
