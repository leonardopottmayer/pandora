import { familyBounds, type GraphFamily, type PositionedNode } from './graphGroups'

/** Regions are drawn behind real links/nodes; the viewport clamp keeps names readable on phones. */
export function drawGraphRegions(
  ctx: CanvasRenderingContext2D,
  scale: number,
  regions: { family: GraphFamily; nodes: PositionedNode[] }[],
  textColor: string,
  viewport: { left: number; right: number },
) {
  for (const { family, nodes } of regions) {
    const bounds = familyBounds(nodes, Math.max(28, 24 / scale))
    if (!bounds) continue
    ctx.save()
    ctx.beginPath()
    ctx.roundRect(bounds.x, bounds.y, bounds.width, bounds.height, 20)
    ctx.fillStyle = family.color
    ctx.globalAlpha = 0.08
    ctx.fill()
    ctx.globalAlpha = 0.55
    ctx.strokeStyle = family.color
    ctx.lineWidth = 1.5 / scale
    ctx.stroke()
    ctx.globalAlpha = 1
    ctx.font = `600 ${13 / scale}px sans-serif`
    ctx.fillStyle = textColor
    ctx.textAlign = 'left'
    ctx.textBaseline = 'bottom'
    const maxWidth = Math.min(220 / scale, viewport.right - viewport.left)
    let label = family.name
    while (label.length > 1 && ctx.measureText(label).width > maxWidth) label = label.slice(0, -1)
    if (label !== family.name) label = `${label.slice(0, -1)}…`
    const x = Math.max(viewport.left, Math.min(bounds.x + 8 / scale, viewport.right - ctx.measureText(label).width))
    ctx.fillText(label, x, bounds.y - 4 / scale)
    ctx.restore()
  }
}
