// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useMemo, useRef, useState } from 'react';
import {
  breadcrumb, childrenOf, edgesWithin, exportMermaid, relatedEdges, rootNode
} from './graph';
import type { GraphEdge, GraphNode, GraphSnapshot } from './graph';

type Point = { x: number; y: number };
type FetchState = 'idle' | 'loading' | 'loaded' | 'error';

const blockWidth = 224;
const blockHeight = 86;
const gapX = 64;
const gapY = 92;

function layoutNodes(nodes: GraphNode[]) {
  const columns = Math.min(3, Math.max(1, Math.ceil(Math.sqrt(nodes.length))));
  const positions = new Map<string, Point>();
  nodes.forEach((node, index) => {
    positions.set(node.symbolId, {
      x: 56 + (index % columns) * (blockWidth + gapX),
      y: 50 + Math.floor(index / columns) * (blockHeight + gapY)
    });
  });
  const rows = Math.ceil(nodes.length / columns);
  return {
    positions,
    width: Math.max(400, 80 + columns * (blockWidth + gapX)),
    height: Math.max(300, 80 + rows * (blockHeight + gapY))
  };
}

export function App() {
  const [project, setProject] = useState('');
  const [revision, setRevision] = useState('');
  const [graph, setGraph] = useState<GraphSnapshot | null>(null);
  const [status, setStatus] = useState<FetchState>('idle');
  const [errorMessage, setErrorMessage] = useState('');
  const [scopeId, setScopeId] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [searchText, setSearchText] = useState('');
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState<Point>({ x: 0, y: 0 });
  const [contextNotice, setContextNotice] = useState('');
  const lastRequest = useRef<AbortController | null>(null);
  const canvasRef = useRef<SVGSVGElement>(null);
  const dragRef = useRef<{ startX: number; startY: number; panX: number; panY: number } | null>(null);

  const scope = graph && (scopeId ? graph.nodes.find(n => n.symbolId === scopeId) : rootNode(graph));
  const visibleNodes = useMemo(
    () => graph && scope ? childrenOf(graph, scope.symbolId) : [],
    [graph, scope]
  );
  const visibleEdges = useMemo(
    () => graph ? edgesWithin(graph, visibleNodes) : [], [graph, visibleNodes]
  );
  const coordinates = useMemo(() => layoutNodes(visibleNodes), [visibleNodes]);
  const selected = useMemo(
    () => graph?.nodes.find(node => node.symbolId === selectedId) ?? null,
    [graph, selectedId]
  );
  const selectedEdges = useMemo(
    () => graph && selectedId ? relatedEdges(graph, selectedId) : [],
    [graph, selectedId]
  );
  const trail = useMemo(
    () => graph && scope ? breadcrumb(graph, scope.symbolId) : [],
    [graph, scope]
  );

  const resetView = useCallback(() => {
    setZoom(1);
    setPan({ x: 0, y: 0 });
  }, []);

  const enterScope = useCallback((id: string) => {
    setScopeId(id);
    setSelectedId(null);
    setContextNotice('');
    resetView();
  }, [resetView]);

  const goBack = useCallback(() => {
    if (!graph || !scope) return;
    if (scope.parentSymbolId) enterScope(scope.parentSymbolId);
  }, [graph, scope, enterScope]);

  async function loadGraph(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    lastRequest.current?.abort();
    const controller = new AbortController();
    lastRequest.current = controller;
    setStatus('loading');
    setGraph(null);
    setSelectedId(null);
    setScopeId(null);
    setErrorMessage('');
    setContextNotice('');
    const name = project.trim();
    const sha = revision.trim();
    if (!name || !sha) {
      setStatus('error');
      setErrorMessage('Enter the exact stored project name and source revision.');
      return;
    }
    try {
      const response = await fetch('/api/graphs/' + encodeURIComponent(name) + '/' +
        encodeURIComponent(sha), { signal: controller.signal });
      if (!response.ok) {
        throw new Error(response.status === 404
          ? 'No stored graph exists for that project and revision.'
          : 'Graph retrieval failed: HTTP ' + response.status);
      }
      const result: GraphSnapshot = await response.json() as GraphSnapshot;
      if (result.project !== name || result.revision !== sha ||
          !Array.isArray(result.nodes) || !Array.isArray(result.edges)) {
        throw new Error('The API returned a graph with an unexpected identity or invalid shape.');
      }
      // Zero fabricated fallback nodes: empty result is shown as an error.
      if (!rootNode(result)) throw new Error('No canonical graph root exists in this revision.');
      setGraph(result);
      setScopeId(rootNode(result)!.symbolId);
      setStatus('loaded');
      resetView();
    } catch (error) {
      if (controller.signal.aborted) return;
      setStatus('error');
      setErrorMessage(error instanceof Error ? error.message : 'Could not load this graph.');
    }
  }

  const chooseSearch = useCallback(() => {
    if (!graph) return;
    const needle = searchText.trim().toLocaleLowerCase();
    if (!needle) return;
    const matches = graph.nodes.filter(node => (
      node.symbolId.toLocaleLowerCase().includes(needle) ||
      node.label.toLocaleLowerCase().includes(needle)
    ));
    const found = matches[0];
    if (!found) {
      setContextNotice('No matching symbol in this exact graph revision.');
      return;
    }
    enterScope(found.parentSymbolId ?? found.symbolId);
    setSelectedId(found.symbolId);
    setContextNotice(matches.length > 1
      ? matches.length + ' symbols matched; showing the first, with exact ID below.' : '');
  }, [graph, searchText, enterScope]);

  function copyMermaid() {
    if (!graph) return;
    const text = exportMermaid(graph, visibleNodes);
    void navigator.clipboard.writeText(text).then(
      () => setContextNotice('Mermaid copied. It contains only relationships visible in this scope.'),
      () => setContextNotice('Clipboard unavailable; use a local HTTPS/localhost environment.')
    );
  }

  function onPointerDown(event: React.PointerEvent<SVGSVGElement>) {
    if (event.target !== canvasRef.current && !(event.target instanceof SVGRectElement &&
        event.target.dataset.background === 'true')) return;
    dragRef.current = {
      startX: event.clientX, startY: event.clientY, panX: pan.x, panY: pan.y
    };
    event.currentTarget.setPointerCapture(event.pointerId);
  }

  function onPointerMove(event: React.PointerEvent<SVGSVGElement>) {
    if (!dragRef.current) return;
    setPan({
      x: dragRef.current.panX + event.clientX - dragRef.current.startX,
      y: dragRef.current.panY + event.clientY - dragRef.current.startY
    });
  }

  function onPointerUp(event: React.PointerEvent<SVGSVGElement>) {
    dragRef.current = null;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
  }

  const nodeById = useMemo(
    () => new Map(visibleNodes.map(node => [node.symbolId, node])),
    [visibleNodes]
  );

  const inspector = selected ? (
    <div className="inspector-content" aria-live="polite">
      <div className="eyebrow">Exact graph selection</div>
      <h3>{selected.label}</h3>
      <p className="muted">{selected.kind}</p>
      <dl className="metadata">
        <dt>Symbol ID</dt><dd className="mono wrap">{selected.symbolId}</dd>
        <dt>Revision</dt><dd className="mono wrap">{selected.evidence.revision}</dd>
        <dt>Evidence mode</dt><dd>{selected.evidence.kind}</dd>
        <dt>Source file</dt><dd className="mono wrap">{selected.evidence.relativeFile || 'Project / solution root'}</dd>
        <dt>Location</dt><dd>{selected.evidence.line}:{selected.evidence.column}</dd>
      </dl>
      <h4>Evidence-backed edges ({selectedEdges.length})</h4>
      {selectedEdges.length === 0 && <p className="muted">No graph edges mapped.</p>}
      <div className="edge-list">
        {selectedEdges.map(edge => (
          <div className="edge-item" key={edge.edgeId}>
            <strong>{edge.relation}</strong>
            <span className="mono wrap">{edge.fromSymbolId}</span>
            <span className="muted">to</span>
            <span className="mono wrap">{edge.toSymbolId}</span>
            <small>{edge.evidence.kind} · {edge.evidence.relativeFile || 'root'}:{edge.evidence.line}</small>
          </div>
        ))}
      </div>
      {graph && childrenOf(graph, selected.symbolId).some(child => child.parentSymbolId === selected.symbolId)
        && <button className="primary" onClick={() => enterScope(selected.symbolId)}>Drill into this symbol</button>}
      <p className="muted small">This selection is not a runtime execution trace. Ask AI, Run Tests and Refactor actions are not implemented yet.</p>
    </div>
  ) : (
    <div className="inspector-content">
      <h3>Evidence inspector</h3>
      <p className="muted">Select a node on the actual graph to inspect its exact Symbol ID, source, revision and relations.</p>
    </div>
  );

  return (
    <main className="studio">
      <header className="topbar">
        <div>
          <div className="eyebrow">ARCHPULSE / ENGINEERING GRAPH</div>
          <h1>Architecture Studio <span className="experimental">Experimental</span></h1>
          <p>Source-derived .NET graph. No simulated nodes or unverified runtime paths.</p>
        </div>
        <div className="topbar-info">{graph && <span className="revision-pill">Static · {graph.revision}</span>}</div>
      </header>

      <form className="toolbar" onSubmit={event => { void loadGraph(event); }}>
        <label>Stored project
          <input aria-label="Stored project name" value={project}
            onChange={event => setProject(event.target.value)} placeholder="Billing" required />
        </label>
        <label>Git/source revision
          <input aria-label="Source revision" value={revision}
            onChange={event => setRevision(event.target.value)} placeholder="Exact saved SHA" required />
        </label>
        <button className="primary" type="submit" disabled={status === 'loading'}>{status === 'loading' ? 'Loading…' : 'Open graph'}</button>
      </form>

      {status === 'error' && <div className="error" role="alert">{errorMessage}</div>}
      {!graph && status !== 'error' &&
        <div className="intro"><h2>Load an indexed workspace revision</h2>
          <p>First index a trusted .NET solution with the ArchPulse CLI and store the result, then enter its exact project and revision here. Studio does not generate architecture from guesses.</p>
          <p className="muted">The API and Studio are currently local-only and unauthenticated. Do not expose them to the public network.</p>
        </div>}

      {graph && scope && <>
        <section className="contextbar">
          <nav aria-label="Graph hierarchy" className="breadcrumbs">
            {trail.map((node, i) => <React.Fragment key={node.symbolId}>
              {i > 0 && <span className="separator">/</span>}
              <button type="button" onClick={() => enterScope(node.symbolId)}
                aria-current={i === trail.length - 1 ? 'page' : undefined}>{node.label}</button>
            </React.Fragment>)}
          </nav>
          <div className="context-actions">
            <button type="button" onClick={goBack} disabled={!scope.parentSymbolId}>Up</button>
            <span className="muted small">{graph.nodes.length} nodes · {graph.edges.length} edges in revision</span>
          </div>
        </section>
        <div className="studio-body">
          <section className="canvas-panel" aria-label="Architecture graph">
            <div className="canvas-toolbar">
              <label className="search-label">Find exact symbol
                <input value={searchText} onChange={e => setSearchText(e.target.value)}
                  onKeyDown={e => { if (e.key === 'Enter') chooseSearch(); }}
                  placeholder="Class, method or Symbol ID" />
              </label>
              <button onClick={chooseSearch}>Find</button>
              <div className="toolbar-spacer" />
              <button aria-label="Zoom out" onClick={() => setZoom(z => Math.max(.35, Math.round((z - .15) * 100) / 100))}>−</button>
              <span className="zoom-value">{Math.round(zoom * 100)}%</span>
              <button aria-label="Zoom in" onClick={() => setZoom(z => Math.min(2.4, Math.round((z + .15) * 100) / 100))}>+</button>
              <button onClick={resetView}>Reset</button>
              <button onClick={copyMermaid}>Copy Mermaid</button>
            </div>
            <div className="diagram-frame">
              <svg ref={canvasRef} aria-label={'Architecture diagram: ' + scope.label}
                role="img" tabIndex={0} viewBox="0 0 1000 650"
                onKeyDown={event => { if (event.key === 'Escape') goBack(); }}
                onPointerDown={onPointerDown} onPointerMove={onPointerMove}
                onPointerUp={onPointerUp} onPointerCancel={onPointerUp}>
                <defs>
                  <pattern id="grid-dots" width="24" height="24" patternUnits="userSpaceOnUse">
                    <circle cx="1.5" cy="1.5" r="1.2" fill="#cad5e6" />
                  </pattern>
                  <marker id="arrowhead" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto">
                    <path d="M0,0 L7,3.5 L0,7" fill="none" stroke="#6a819a" strokeWidth="1.3" />
                  </marker>
                </defs>
                <rect width="1000" height="650" fill="url(#grid-dots)" data-background="true" />
                <g transform={'translate(' + pan.x + ' ' + pan.y + ') scale(' + zoom + ')'}>
                  {visibleEdges.map((edge: GraphEdge) => {
                    const from = coordinates.positions.get(edge.fromSymbolId);
                    const to = coordinates.positions.get(edge.toSymbolId);
                    if (!from || !to) return null;
                    return <g key={edge.edgeId}>
                      <line x1={from.x + blockWidth / 2} y1={from.y + blockHeight}
                        x2={to.x + blockWidth / 2} y2={to.y}
                        stroke="#8095ad" strokeWidth="1.7" markerEnd="url(#arrowhead)" />
                      <text x={(from.x + to.x + blockWidth) / 2}
                        y={(from.y + to.y + blockHeight) / 2 - 8}
                        fontSize="13" fill="#41536d" textAnchor="middle">{edge.relation}</text>
                    </g>;
                  })}
                  {visibleNodes.map((node: GraphNode) => {
                    const p = coordinates.positions.get(node.symbolId)!;
                    const active = node.symbolId === selectedId;
                    const expandable = graph.nodes.some(n => n.parentSymbolId === node.symbolId);
                    return <g key={node.symbolId}
                      role="button" tabIndex={0}
                      aria-label={node.kind + ': ' + node.label}
                      aria-pressed={active}
                      onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault(); setSelectedId(node.symbolId);
                      } }}
                      onClick={() => { setSelectedId(node.symbolId); setContextNotice(''); }}
                      onDoubleClick={() => { if (expandable) enterScope(node.symbolId); }}
                      className="graph-node"
                      transform={'translate(' + p.x + ' ' + p.y + ')'}>
                      <rect width={blockWidth} height={blockHeight} rx="12"
                        stroke={active ? '#1477ad' : '#a8b8ce'}
                        strokeWidth={active ? 3 : 1.5}
                        fill={active ? '#e0f1ff' : '#fff'} />
                      <text x="14" y="23" fill="#50617b" fontSize="12">{node.kind.toUpperCase()}</text>
                      <text x="14" y="48" fill="#15263e" fontSize="15" fontWeight="600">
                        {node.label.length > 25 ? node.label.slice(0, 23) + '…' : node.label}
                      </text>
                      <text x="14" y="69" fill="#597088" fontSize="11">
                        {node.evidence.kind} · {expandable ? 'Double-click to drill' : 'Select to inspect'}
                      </text>
                    </g>;
                  })}
                </g>
              </svg>
              {visibleNodes.length === 0 &&
                <div className="empty-scope">No child symbols are mapped in this scope.</div>}
            </div>
            <div className="canvas-footer">
              <span>Scope: <strong>{scope.kind} / {scope.label}</strong></span>
              <span>Visible: {visibleNodes.length} nodes · {visibleEdges.length} in-scope edges</span>
            </div>
          </section>
          <aside className="inspector" aria-label="Selected node evidence">{inspector}</aside>
        </div>
        {contextNotice && <div role="status" className="notice">{contextNotice}</div>}
        {graph.diagnostics.length > 0 &&
          <details className="diagnostics"><summary>Indexer diagnostics ({graph.diagnostics.length})</summary>
            <ul>{graph.diagnostics.map((line, index) => <li key={index}>{line}</li>)}</ul>
          </details>}
        <footer>Showing {nodeById.size} nodes in the current hierarchical scope. Cross-scope and external edges can be examined from the inspector. No runtime instrumentation or AI decisions are asserted.</footer>
      </>}
    </main>
  );
}
