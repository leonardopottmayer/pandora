import type { PageVersionSummaryDto } from '../models'

/** Groups adjacent manual edits after gaps of at most five minutes, without discarding a save. */
export function groupVersions(versions: PageVersionSummaryDto[]): PageVersionSummaryDto[][] {
  const groups: PageVersionSummaryDto[][] = []
  for (const version of versions) {
    const group = groups.at(-1)
    const previous = group?.at(-1)
    if (group && previous && version.source === 'edit' && previous.source === 'edit' &&
      new Date(previous.createdAt).getTime() - new Date(version.createdAt).getTime() <= 5 * 60_000) {
      group.push(version)
    } else groups.push([version])
  }
  return groups
}
