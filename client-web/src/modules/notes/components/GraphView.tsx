import { useEffect, useMemo, useRef, useState } from 'react'
import { theme } from 'antd'
import ForceGraph2D from 'react-force-graph-2d'
import { buildAdjacency, endpointId, nodeLabel, toForceData } from '../lib/graphData'
import type { PageGraphDto } from '../models'

interface GraphViewProps {
  graph: PageGraphDto | undefined
  /** The page the graph is centered on, drawn as the accent node. */
  currentId?: string | null
  /** Clicking a node opens that page. */
  onSelect: (id: string) => void
}

/**
 * The wiki graph on a canvas (d3-force under the hood). Nodes are pages sized by how many edges
 * touch them; hovering one dims everything that is not its immediate neighborhood, which is the
 * only way a graph of any size stays readable.
 *
 * The component fills whatever box it is given — `react-force-graph` needs pixel dimensions, so the
 * container is measured rather than sized in CSS.
 */
export function GraphView({ graph, currentId, onSelect }: GraphViewProps) {
  const { token } = theme.useToken()
  const containerRef = useRef<HTMLDivElement>(null)
  const [size, setSize] = useState({ width: 0, height: 0 })
  const [hoveredId, setHoveredId] = useState<string | null>(null)

  useEffect(() => {
    const element = containerRef.current
    if (!element) return

    const observer = new ResizeObserver(([entry]) =>
      setSize({ width: entry.contentRect.width, height: entry.contentRect.height }),
    )
    observer.observe(element)
    return () => observer.disconnect()
  }, [])

  // Rebuilt only when the payload changes: handing the simulation a new array restarts it, so
  // doing this per render would leave the layout permanently reheating.
  const data = useMemo(() => toForceData(graph), [graph])
  const adjacency = useMemo(() => buildAdjacency(graph), [graph])

  /** With nothing hovered every node is "in focus"; hovering narrows it to the node and its neighbors. */
  function isInFocus(id: string) {
    if (!hoveredId) return true
    return id === hoveredId || (adjacency.get(hoveredId)?.has(id) ?? false)
  }

  function nodeColor(id: string, isArchived: boolean) {
    if (id === currentId) return token.colorPrimary
    return isArchived ? token.colorTextQuaternary : token.colorTextSecondary
  }

  return (
    <div ref={containerRef} style={{ width: '100%', height: '100%', overflow: 'hidden' }}>
      {size.width > 0 && (
        <ForceGraph2D
          width={size.width}
          height={size.height}
          graphData={data}
          backgroundColor="rgba(0,0,0,0)"
          nodeRelSize={4}
          // Degree is capped: one hub page should stand out, not swallow the canvas.
          nodeVal={(node) => 1 + Math.min(Number(node.degree) || 0, 12) * 0.6}
          nodeLabel={(node) => nodeLabel(node.icon, node.title)}
          linkColor={(link) => (isInFocus(endpointId(link.source)) && isInFocus(endpointId(link.target))
            ? token.colorBorder
            : token.colorBorderSecondary)}
          linkLineDash={(link) => (link.kind === 'embed' ? [2, 2] : null)}
          linkDirectionalArrowLength={3}
          linkDirectionalArrowRelPos={1}
          onNodeClick={(node) => onSelect(String(node.id))}
          onNodeHover={(node) => setHoveredId(node ? String(node.id) : null)}
          nodeCanvasObjectMode={() => 'after'}
          nodeCanvasObject={(node, ctx, globalScale) => {
            const id = String(node.id)

            // Below this the labels overlap into noise; the tooltip still names every node.
            if (globalScale < 1.2 && id !== currentId && id !== hoveredId) return

            const focused = isInFocus(id)
            const label = nodeLabel(node.icon, node.title)

            ctx.font = `${12 / globalScale}px sans-serif`
            ctx.textAlign = 'center'
            ctx.textBaseline = 'top'
            ctx.fillStyle = focused ? token.colorText : token.colorTextQuaternary
            ctx.fillText(label, node.x ?? 0, (node.y ?? 0) + 6)
          }}
          nodeColor={(node) =>
            isInFocus(String(node.id))
              ? nodeColor(String(node.id), Boolean(node.isArchived))
              : token.colorTextQuaternary
          }
          cooldownTicks={100}
        />
      )}
    </div>
  )
}
