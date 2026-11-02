import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Flex, Typography, theme } from 'antd'
import { FullscreenOutlined, MinusSquareOutlined, PlusSquareOutlined, ZoomInOutlined, ZoomOutOutlined } from '@ant-design/icons'
import ForceGraph2D, { type ForceGraphMethods } from 'react-force-graph-2d'
import { buildAdjacency, endpointId, nodeLabel, type ForceLink, type ForceNode } from '../lib/graphData'
import { collapsedGraph } from '../lib/collapsedGraph'
import { familyForce, familyForNode, type GraphFamily, type PositionedNode } from '../lib/graphGroups'
import { drawGraphRegions } from '../lib/graphRegions'
import type { PageGraphDto } from '../models'

interface GraphViewProps {
  graph: PageGraphDto | undefined
  /** The page the graph is centered on, drawn as the accent node. */
  currentId?: string | null
  /** A search result to center and keep highlighted without opening the page. */
  focusedId?: string | null
  /** The parent decides whether clicking inspects or opens the page. */
  onSelect: (id: string) => void
  /** Absent is the original free graph; an empty array is grouped mode without configured families. */
  families?: GraphFamily[]
  collapsedFamilyIds?: string[]
  onToggleFamily?: (id: string) => void
}

const NO_FAMILIES: GraphFamily[] = []
const NO_COLLAPSED: string[] = []

/**
 * The wiki graph on a canvas (d3-force under the hood). Nodes are pages sized by how many edges
 * touch them; hovering one dims everything that is not its immediate neighborhood, which is the
 * only way a graph of any size stays readable.
 *
 * The component fills whatever box it is given — `react-force-graph` needs pixel dimensions, so the
 * container is measured rather than sized in CSS.
 */
export function GraphView({ graph, currentId, focusedId, onSelect, families, collapsedFamilyIds = NO_COLLAPSED, onToggleFamily }: GraphViewProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const graphRef = useRef<ForceGraphMethods<ForceNode, ForceLink> | undefined>(undefined)
  const manualCameraRef = useRef(false)
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

  // Rebuild only for graph/group changes; a new array restarts the force simulation.
  const activeFamilies = families ?? NO_FAMILIES
  const data = useMemo(() => collapsedGraph(graph, activeFamilies, collapsedFamilyIds), [graph, activeFamilies, collapsedFamilyIds])
  const adjacency = useMemo(() => buildAdjacency({ nodes: [], edges: data.links.map((link) => ({
    sourceId: endpointId(link.source), targetId: endpointId(link.target), kind: link.kind,
  })) }), [data])
  const locatedId = data.nodes.some((node) => node.id === focusedId) ? focusedId : null
  const hoveredNode = data.nodes.find((node) => node.id === hoveredId)
  const highlightedId = hoveredNode?.id ?? locatedId
  const centerLocatedNode = useCallback(() => {
    const view = graphRef.current
    const node: PositionedNode | undefined = data.nodes.find((candidate) => candidate.id === locatedId)
    if (!view || !node || !Number.isFinite(node.x) || !Number.isFinite(node.y)) return
    view.centerAt(node.x, node.y, 300)
    view.zoom(2, 300)
  }, [locatedId, data])

  useEffect(() => {
    manualCameraRef.current = false
    if (size.width > 0) centerLocatedNode()
  }, [centerLocatedNode, data, size.width])
  const membership = useMemo(() => new Map(data.nodes.map((node) =>
    [node.id, familyForNode(node, activeFamilies)])), [data, activeFamilies])
  const visibleFamilies = useMemo(() => activeFamilies.map((family) => ({
    family, count: graph?.nodes.filter((node) => familyForNode(node, activeFamilies)?.id === family.id).length ?? 0,
  })).filter((entry) => entry.count > 0), [activeFamilies, graph])
  const regions = useMemo(() => activeFamilies.map((family) => ({
    family, nodes: data.nodes.filter((node) => !node.familyId && membership.get(node.id)?.id === family.id),
  })).filter((region) => region.nodes.length > 0), [activeFamilies, data, membership])
  const hoveredFamilies = hoveredNode
    ? activeFamilies.filter((family) => family.id === hoveredNode.familyId || family.tagIds.some((id) => hoveredNode.tagIds.includes(id)))
    : []

  useEffect(() => {
    const view = graphRef.current
    if (!view || size.width === 0 || visibleFamilies.length === 0) return
    const linkForce = view.d3Force('link')
    const originalStrength = linkForce?.strength()
    // Cross-family links remain visible, but no longer pull different regions into the same knot.
    linkForce?.strength((link: ForceLink) =>
      membership.get(endpointId(link.source))?.id === membership.get(endpointId(link.target))?.id ? 0.5 : 0.025)
    view.d3Force('families', familyForce(activeFamilies))
    view.d3ReheatSimulation()
    return () => {
      view.d3Force('families', null)
      if (originalStrength) linkForce?.strength(originalStrength)
      view.d3ReheatSimulation()
    }
  }, [activeFamilies, membership, data, visibleFamilies, size.width])

  /** Hover temporarily takes priority over the persistent search highlight. */
  function isInFocus(id: string) {
    if (!highlightedId) return true
    return id === highlightedId || (adjacency.get(highlightedId)?.has(id) ?? false)
  }

  function nodeColor(id: string, isArchived: boolean) {
    if (id === currentId || id === locatedId) return token.colorPrimary
    return isArchived ? token.colorTextQuaternary : membership.get(id)?.color ?? token.colorTextSecondary
  }

  function changeZoom(factor: number) {
    const view = graphRef.current
    if (!view) return
    manualCameraRef.current = true
    view.zoom(view.zoom() * factor, 300)
  }

  function fitGraph() {
    manualCameraRef.current = true
    graphRef.current?.zoomToFit(300, 55)
  }

  return (
    <div ref={containerRef} style={{ position: 'relative', width: '100%', height: '100%', overflow: 'hidden' }}>
      {size.width > 0 && (
        <ForceGraph2D
          ref={graphRef}
          width={size.width}
          height={size.height}
          graphData={data}
          backgroundColor="rgba(0,0,0,0)"
          onRenderFramePre={(ctx, scale) => {
            const view = graphRef.current
            if (!view || regions.length === 0) return
            drawGraphRegions(ctx, scale, regions, token.colorText, {
              left: view.screen2GraphCoords(12, 0).x,
              right: view.screen2GraphCoords(size.width - 12, 0).x,
            })
          }}
          nodeRelSize={4}
          // Degree is capped: one hub page should stand out, not swallow the canvas.
          nodeVal={(node) => node.familyId ? 4 + Math.min(Number(node.memberCount), 40) * 0.4 : 1 + Math.min(Number(node.degree) || 0, 12) * 0.6}
          nodeLabel={(node) => {
            if (!node.familyId) return nodeLabel(node.icon, node.title)
            const label = document.createElement('span')
            label.textContent = `${node.title} · ${node.memberCount}`
            return label.innerHTML
          }}
          linkLabel={(link) => link.count ? t('notes.graphGroups.linkCount', { count: link.count }) : ''}
          linkColor={(link) => {
            if (!highlightedId) return token.colorBorder
            return endpointId(link.source) === highlightedId || endpointId(link.target) === highlightedId
              ? token.colorPrimary : token.colorBorderSecondary
          }}
          linkLineDash={(link) => (link.kind === 'embed' ? [2, 2] : null)}
          linkDirectionalArrowLength={3}
          linkDirectionalArrowRelPos={1}
          onNodeClick={(node) => node.familyId ? onToggleFamily?.(String(node.familyId)) : onSelect(String(node.id))}
          onNodeHover={(node) => setHoveredId(node ? String(node.id) : null)}
          nodeCanvasObjectMode={() => 'after'}
          nodeCanvasObject={(node, ctx, globalScale) => {
            const id = String(node.id)

            // Below this the labels overlap into noise; the tooltip still names every node.
            if (globalScale < 1.2 && !node.familyId && id !== currentId && id !== hoveredId && id !== locatedId) return

            const focused = isInFocus(id)
            let label = node.familyId ? `${node.title} · ${node.memberCount}` : nodeLabel(node.icon, node.title)

            ctx.font = `${12 / globalScale}px sans-serif`
            if (node.familyId) {
              let name = String(node.title)
              const maxWidth = Math.min(240, size.width - 24) / globalScale
              while (name.length > 1 && ctx.measureText(`${name}… · ${node.memberCount}`).width > maxWidth) name = name.slice(0, -1)
              if (name !== node.title) label = `${name}… · ${node.memberCount}`
            }
            ctx.textAlign = 'center'
            ctx.textBaseline = 'top'
            ctx.fillStyle = focused ? token.colorText : token.colorTextQuaternary
            ctx.fillText(label, node.x ?? 0, (node.y ?? 0) + (node.familyId ? 20 : 6))
          }}
          nodeColor={(node) =>
            isInFocus(String(node.id))
              ? nodeColor(String(node.id), Boolean(node.isArchived))
              : token.colorTextQuaternary
          }
          cooldownTicks={100}
          onEngineStop={() => {
            if (manualCameraRef.current) return
            if (locatedId) centerLocatedNode()
            else if (visibleFamilies.length > 0) graphRef.current?.zoomToFit(300, 55)
          }}
        />
      )}
      <Flex role="group" aria-label={t('notes.graphControls.label')} gap={4} style={{ position: 'absolute', top: 12, right: 12 }}>
        <Button
          size="large"
          icon={<ZoomInOutlined />}
          aria-label={t('notes.graphControls.zoomIn')}
          title={t('notes.graphControls.zoomIn')}
          disabled={size.width === 0 || data.nodes.length === 0}
          onClick={() => changeZoom(1.25)}
        />
        <Button
          size="large"
          icon={<ZoomOutOutlined />}
          aria-label={t('notes.graphControls.zoomOut')}
          title={t('notes.graphControls.zoomOut')}
          disabled={size.width === 0 || data.nodes.length === 0}
          onClick={() => changeZoom(1 / 1.25)}
        />
        <Button
          size="large"
          icon={<FullscreenOutlined />}
          aria-label={t('notes.graphControls.fit')}
          title={t('notes.graphControls.fit')}
          disabled={size.width === 0 || data.nodes.length === 0}
          onClick={fitGraph}
        />
      </Flex>
      {families && (
        <div style={{ position: 'absolute', bottom: 12, left: 12, right: 12, pointerEvents: 'none' }}>
          {families.length === 0 && <Typography.Text type="secondary">{t('notes.graphGroups.empty')}</Typography.Text>}
          {hoveredId && (
            <Typography.Paragraph style={{ marginBottom: 4 }}>
              {hoveredFamilies.map((family) => family.name).join(' · ') || t('notes.graphGroups.ungrouped')}
            </Typography.Paragraph>
          )}
          <Flex gap={6} wrap>
            {visibleFamilies.map(({ family, count }) => (
              <Flex key={family.id} gap={2} style={{ pointerEvents: 'auto', maxWidth: '100%' }}>
                <Button
                  size="small"
                  style={{ borderColor: family.color, minWidth: 0, flex: 1 }}
                  title={family.name}
                  onClick={() => {
                    manualCameraRef.current = true
                    graphRef.current?.zoomToFit(300, 40, (node) => membership.get(String(node.id))?.id === family.id)
                  }}
                >
                  <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>{family.name} ({count})</span>
                </Button>
                {onToggleFamily && <Button
                  size="small"
                  icon={collapsedFamilyIds.includes(family.id) ? <PlusSquareOutlined /> : <MinusSquareOutlined />}
                  aria-label={t(collapsedFamilyIds.includes(family.id) ? 'notes.graphGroups.expand' : 'notes.graphGroups.collapse', { name: family.name })}
                  title={t(collapsedFamilyIds.includes(family.id) ? 'notes.graphGroups.expand' : 'notes.graphGroups.collapse', { name: family.name })}
                  onClick={() => onToggleFamily(family.id)}
                />}
              </Flex>
            ))}
          </Flex>
        </div>
      )}
    </div>
  )
}
