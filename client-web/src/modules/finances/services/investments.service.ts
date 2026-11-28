import { apiClient } from '@/lib/api/client'
import type {
  AddPositionMovementRequest,
  AssetBehavior,
  AssetClassDto,
  AssetDto,
  CreateAssetRequest,
  CreatePositionRequest,
  InvestmentsSummaryDto,
  PositionDetailDto,
  PositionDto,
  PositionValuationDto,
} from '../models'

const BASE = '/api/v1.0/finances'

export async function listAssetClasses(): Promise<AssetClassDto[]> {
  const { data } = await apiClient.get<AssetClassDto[]>(`${BASE}/asset-classes`)
  return data
}

export async function createAssetClass(body: { name: string; behavior: AssetBehavior }): Promise<AssetClassDto> {
  const { data } = await apiClient.post<AssetClassDto>(`${BASE}/asset-classes`, body)
  return data
}

export async function renameAssetClass(id: string, name: string): Promise<AssetClassDto> {
  const { data } = await apiClient.put<AssetClassDto>(`${BASE}/asset-classes/${id}`, { name })
  return data
}

export async function setAssetClassArchived(id: string, archived: boolean): Promise<AssetClassDto> {
  const { data } = await apiClient.post<AssetClassDto>(`${BASE}/asset-classes/${id}/${archived ? 'archive' : 'unarchive'}`)
  return data
}

export async function deleteAssetClass(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/asset-classes/${id}`)
}

export async function deleteAsset(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/assets/${id}`)
}

export async function listAssets(): Promise<AssetDto[]> {
  const { data } = await apiClient.get<AssetDto[]>(`${BASE}/assets`)
  return data
}

export async function createAsset(body: CreateAssetRequest): Promise<AssetDto> {
  const { data } = await apiClient.post<AssetDto>(`${BASE}/assets`, body)
  return data
}

/** The currency is fixed at creation; the class can change to one with the same behavior. */
export async function updateAsset(
  id: string,
  body: Pick<CreateAssetRequest, 'name' | 'code' | 'isin' | 'issuer' | 'assetClassId'>,
): Promise<AssetDto> {
  const { data } = await apiClient.put<AssetDto>(`${BASE}/assets/${id}`, body)
  return data
}

export async function setAssetArchived(id: string, archived: boolean): Promise<AssetDto> {
  const { data } = await apiClient.post<AssetDto>(`${BASE}/assets/${id}/${archived ? 'archive' : 'unarchive'}`)
  return data
}

export interface ListPositionsParams {
  accountId?: string
  includeClosed?: boolean
}

export async function listPositions(params: ListPositionsParams = {}): Promise<PositionDto[]> {
  const { data } = await apiClient.get<PositionDto[]>(`${BASE}/positions`, {
    params: { accountId: params.accountId, includeClosed: params.includeClosed },
  })
  return data
}

export async function getPosition(id: string): Promise<PositionDetailDto> {
  const { data } = await apiClient.get<PositionDetailDto>(`${BASE}/positions/${id}`)
  return data
}

export async function createPosition(body: CreatePositionRequest): Promise<PositionDto> {
  const { data } = await apiClient.post<PositionDto>(`${BASE}/positions`, body)
  return data
}

export async function addPositionMovement(id: string, body: AddPositionMovementRequest): Promise<PositionDto> {
  const { data } = await apiClient.post<PositionDto>(`${BASE}/positions/${id}/movements`, body)
  return data
}

/** Undoes the latest movement; `false` means it was the only one and the position is gone. */
export async function undoPositionMovement(id: string): Promise<boolean> {
  const { data } = await apiClient.delete<boolean>(`${BASE}/positions/${id}/movements/latest`)
  return data
}

export async function reportPositionValue(id: string, valuedOn: string, value: number): Promise<PositionValuationDto[]> {
  const { data } = await apiClient.post<PositionValuationDto[]>(`${BASE}/positions/${id}/valuations`, { valuedOn, value })
  return data
}

export async function reportPositionValues(
  valuedOn: string,
  items: { positionId: string; value: number }[],
): Promise<PositionValuationDto[]> {
  const { data } = await apiClient.post<PositionValuationDto[]>(`${BASE}/positions/valuations`, { valuedOn, items })
  return data
}

export async function getInvestmentsSummary(): Promise<InvestmentsSummaryDto> {
  const { data } = await apiClient.get<InvestmentsSummaryDto>(`${BASE}/investments/summary`)
  return data
}
