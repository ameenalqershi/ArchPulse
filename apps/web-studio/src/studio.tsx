// SPDX-License-Identifier: Apache-2.0
import { useEffect, useMemo, useState } from "react";
import {
  Background, BackgroundVariant, Controls, MiniMap, ReactFlow, ReactFlowProvider,
  type Edge, type Node
} from "@xyflow/react";

type SourceSpan = {
  file: string; startLine: number; startColumn: number; endLine: number; endColumn: number;
};
type Provenance = {
  revision: string; kind: number; analyzer: string; symbolKey: string | null;
  source: SourceSpan | null;
};
type GraphNode = { id: string; kind: number; name: string; evidence: Provenance };
type GraphEdge = { id: string; from: string; to: string; kind: number; evidence: Provenance };
type GraphSnapshot = {
  revision: string; nodes: GraphNode[]; edges: GraphEdge[]; diagnostics: string[];
};
const NODE_KINDS = [
  "Repository", "Solution", "Project", "Namespace", "Class", "Interface",
  "Struct", "Record", "Method", "Property", "External dependency"
];
const EDGE_KINDS = ["Contains", "Calls", "Implements", "DependsOn"];
const EVIDENCE_KINDS = ["Unknown", "Proposed", "Static", "Observed", "Inferred", "Verified"];

function Canvas({ graph, selectedId, onSelect, search, filter }: {
  graph: GraphSnapshot;
  selectedId: string | null;
  onSelect: (id: string) => void;
  search: string;
  filter: number | null;
}) {
  const { nodes, edges } = useMemo(() => {
    const filtered = graph.nodes.filter(n => filter === null || n.kind === filter);
    const totals = new Map<number, number>();
    const keep = new Set(filtered.map(n => n.id));
    const displayedNodes: Node[] = filtered.map(n => {
      const vertical = totals.get(n.kind) ?? 0;
      totals.set(n.kind, vertical + 1);
      const match = search.trim().length === 0 ||
        n.name.toLowerCase().includes(search.trim().toLowerCase()) ||
        n.evidence.symbolKey?.toLowerCase().includes(search.trim().toLowerCase());
      return {
        id: n.id,
        data: { label: n.name.split(".").slice(-2).join(".") },
        position: { x: n.kind * 260, y: vertical * 110 },
        draggable: false,
        selectable: true,
        style: {
          background: n.id === selectedId ? "#164a79" : "#152335",
          color: "#e5eef7",
          border: n.id === selectedId ? "2px solid #68baff" : "1px solid #344a61",
          boxShadow: n.id === selectedId ? "0 0 15px #205886" : undefined,
          borderRadius: 12, padding: 12, fontSize: 12, width: 210,
          opacity: match ? 1 : .18
        }
      };
    });
    const displayedEdges: Edge[] = graph.edges.filter(e => keep.has(e.from) && keep.has(e.to))
      .map(e => ({
        id: e.id,
        source: e.from, target: e.to,
        label: EDGE_KINDS[e.kind] ?? String(e.kind),
        animated: e.kind === 1 && (e.from === selectedId || e.to === selectedId),
        style: { stroke: e.from === selectedId || e.to === selectedId ? "#61b8ff" : "#496078" },
        labelStyle: { fill: "#8daac6", fontSize: 9 }
      }));
    return { nodes: displayedNodes, edges: displayedEdges };
  }, [graph, selectedId, search, filter]);

  return <ReactFlow nodes={nodes} edges={edges} fitView minZoom={0.12} maxZoom={3}
    nodesDraggable={false} nodesConnectable={false} proOptions={{ hideAttribution: false }}
    onNodeClick={(_, node) => onSelect(node.id)}>
    <Controls showInteractive={false}/>
    <MiniMap nodeColor="#5f8cba" pannable zoomable/>
    <Background variant={BackgroundVariant.Dots} color="#304050" gap={24}/>
  </ReactFlow>;
}

export function App() {
  const [graph, setGraph] = useState<GraphSnapshot | null>(null);
  const [error, setError] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<number | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetch("/api/v1/graph", { signal: controller.signal })
      .then(async r => {
        if (!r.ok) throw new Error("Graph endpoint returned HTTP " + r.status);
        return await r.json() as GraphSnapshot;
      })
      .then(g => {
        if (!g.revision || !Array.isArray(g.nodes) || !Array.isArray(g.edges))
          throw new Error("The API returned an invalid graph contract");
        setGraph(g);
      })
      .catch((e: unknown) => {
        if (e instanceof Error && e.name === "AbortError") return;
        setError(e instanceof Error ? e.message : "Unknown graph loading error");
      });
    return () => controller.abort();
  }, []);

  const selected = graph?.nodes.find(n => n.id === selectedId);
  const relations = graph?.edges.filter(e => e.from === selectedId || e.to === selectedId) ?? [];
  const paths = useMemo(() => {
    if (!graph || !selectedId) return [];
    const path: GraphNode[] = [];
    const visited = new Set<string>();
    let current = selectedId;
    while (!visited.has(current)) {
      visited.add(current);
      const node = graph.nodes.find(n => n.id === current);
      if (!node) break;
      path.unshift(node);
      const parent = graph.edges.find(e => e.kind === 0 && e.to === current);
      if (!parent) break;
      current = parent.from;
    }
    return path;
  }, [graph, selectedId]);
  const getName = (id: string) => graph?.nodes.find(n => n.id === id)?.name ?? id;

  return <div className="layout">
    <header className="toolbar">
      <div className="brand"><span className="brandmark">A</span>
        <div><strong>ArchPulse</strong><small>Engineering Studio · static revision snapshot</small></div>
      </div>
      <div className="revision">{graph ? graph.revision.slice(0, 12) : "Not loaded"}</div>
    </header>
    <div className="content">
      <aside className="left-pane">
        <h3>Explore architecture</h3>
        <label htmlFor="search">Search indexed symbols</label>
        <input id="search" placeholder="Symbol or method…" value={search}
          onChange={e => setSearch(e.target.value)}/>
        <label htmlFor="kind-filter">Graph level</label>
        <select id="kind-filter" value={filter ?? "all"}
          onChange={e => setFilter(e.target.value === "all" ? null : Number(e.target.value))}>
          <option value="all">All indexed levels</option>
          {NODE_KINDS.map((kind, i) => <option key={kind} value={i}>{kind}</option>)}
        </select>
        {graph && <section className="metrics">
          <div><strong>{graph.nodes.length}</strong><small>Nodes</small></div>
          <div><strong>{graph.edges.length}</strong><small>Edges</small></div>
          <div><strong>{graph.diagnostics.length}</strong><small>Unknowns</small></div>
        </section>}
        <h4>Evidence rules</h4>
        <p>Solid links are sourced from the loaded graph revision. A missing call trace is not evidence of no execution.</p>
        <h4>Diagnostics</h4>
        <div className="diagnostics">
          {graph?.diagnostics.length === 0 ? "No analyzer diagnostics reported." :
            graph?.diagnostics.map((d, i) => <p key={i}>{d}</p>)}
        </div>
      </aside>
      <main className="canvas-container" aria-label="Architecture diagram">
        {error && <div className="error" role="alert">Failed to load the real graph: {error}. Start the local ArchPulse.Server with a valid snapshot.</div>}
        {!graph && !error && <div className="loading">Loading actual indexed architecture…</div>}
        {graph && <>
          <div className="canvas-top">
            <span>Graph revision: {graph.revision.slice(0, 12)}</span>
            <a href="/api/v1/graph/mermaid" target="_blank" rel="noreferrer">Open Mermaid source</a>
          </div>
          <ReactFlowProvider>
            <Canvas graph={graph} search={search} filter={filter}
              selectedId={selectedId} onSelect={setSelectedId}/>
          </ReactFlowProvider>
        </>}
      </main>
      <aside className="right-pane">
        <h3>Evidence inspector</h3>
        {!selected && <p>Select a real graph node to inspect its exact identity, source, and relationships.</p>}
        {selected && <>
          <div className="type-tag">{NODE_KINDS[selected.kind] ?? selected.kind}</div>
          <h4 className="symbol-title">{selected.name}</h4>
          <div className="trail" aria-label="Architecture breadcrumb">{paths.map((p, i) =>
            <button key={p.id} onClick={() => setSelectedId(p.id)}>
              {i > 0 ? "› " : ""}{p.name.split(".").pop()}
            </button>)}
          </div>
          <dl>
            <dt>Symbol ID</dt><dd className="mono">{selected.id}</dd>
            <dt>Evidence</dt><dd>{EVIDENCE_KINDS[selected.evidence.kind] ?? "Unknown"}</dd>
            <dt>Revision</dt><dd className="mono">{selected.evidence.revision}</dd>
            <dt>Source</dt><dd className="mono">{selected.evidence.source ?
              selected.evidence.source.file + ":" + selected.evidence.source.startLine :
              "NotCaptured"}</dd>
            <dt>Semantic key</dt><dd className="mono">{selected.evidence.symbolKey ?? "Unknown"}</dd>
            <dt>Analyzer</dt><dd>{selected.evidence.analyzer}</dd>
          </dl>
          <button className="copy-button"
            onClick={() => void navigator.clipboard.writeText(selected.id)}>Copy exact Symbol ID</button>
          <h4>Linked relationships ({relations.length})</h4>
          <div className="relations">{relations.map(e => <button key={e.id}
            onClick={() => setSelectedId(e.from === selectedId ? e.to : e.from)}>
            <span>{EDGE_KINDS[e.kind]} · {e.from === selectedId ? "outgoing" : "incoming"}</span>
            <strong>{getName(e.from === selectedId ? e.to : e.from)}</strong>
          </button>)}</div>
        </>}
        <div className="limitation">Initial vertical slice: live indexing, Test Journey, MCP Apps, agent actions and Futures are not enabled here.</div>
      </aside>
    </div>
  </div>;
}
