import { apiClient } from '@/lib/api/client'
import type { Device, DeviceRegistration } from '../models'

const DEVICES_BASE = '/api/v1/identity/devices'

export async function listDevices(): Promise<Device[]> {
  const { data } = await apiClient.get<Device[]>(DEVICES_BASE)
  return data
}

/** Pairs a device; the response carries its key, once. */
export async function registerDevice(input: {
  name: string
  platform: Device['platform']
  form: Device['form']
  scopes: string[]
}): Promise<DeviceRegistration> {
  const { data } = await apiClient.post<DeviceRegistration>(DEVICES_BASE, input)
  return data
}

export async function revokeDevice(id: string): Promise<void> {
  await apiClient.delete(`${DEVICES_BASE}/${id}`)
}
