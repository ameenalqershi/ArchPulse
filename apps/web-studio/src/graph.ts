// SPDX-License-Identifier: Apache-2.0
export type EvidenceKind =
  'Designed' | 'Static' | 'Observed' | 'Inferred' | 'Verified' | 'Unknown';

export type GraphNodeKind =
  'Solution' | 'Project' | 'Namespace' | 'Class' | 'Interface' |
  'Struct' | 'Record' | 'Enum' | 'Method' | 'Property';

export interface SourceEvidence {
  revision: string;
  relativeFile: string;
  line: number;
  column: number;
  kind: EvidenceKind;
}

export interface GraphNode {
  symbolId: string;
  kind: GraphNodeKind;
  label: string;
  parentSymbolId: string | null;
  evidence: SourceEvidence;
}

export interface GraphEdge {
  edgeId: string;
  fromSymbolId: string;
  toSymbolId: string;
  relation: string;
  evidence: SourceEvidence;
}

export interface GraphSnapshot {
  project: string;
  revision: string;
  nodes: GraphNode[];
  edges: GraphEdge[];
  diagnostics: string[];
}

export function rootNode(graph: GraphSnapshot): GraphNode | undefined {
  return graph.nodes.find(node => node.kind === 'Solution' && node.parentSymbolId === null)
    ?? graph.nodes.find(node => node.parentSymbolId === null);
}

export function childrenOf(graph: GraphSnapshot, scopeId: string): GraphNode[] {
  const children = graph.nodes.filter(node => node.parentSymbolId === scopeId);
  const selected = children.length === 0
    ? graph.nodes.filter(node => node.symbolId === scopeId)
    : children;
  return selected.sort((a, b) => a.kind.localeCompare(b.kind) || a.symbolId.localeCompare(b.symbolId));
}

export function breadcrumb(graph: GraphSnapshot, scopeId: string): GraphNode[] {
  const nodes = new Map(graph.nodes.map(node => [node.symbolId, node]));
  const trail: GraphNode[] = [];
  const visited = new Set<string>();
  let cursor: string | null = scopeId;
  while (cursor && nodes.has(cursor)) {
    if (visited.has(cursor)) throw new Error('Cycle in canonical graph hierarchy');
    visited.add(cursor);
    const node: GraphNode = nodes.get(cursor)!;
    trail.unshift(node);
    cursor = node.parentSymbolId;
  }
  return trail;
}

export function relatedEdges(graph: GraphSnapshot, symbolId: string): GraphEdge[] {
  return graph.edges.filter(edge => edge.fromSymbolId === symbolId || edge.toSymbolId === symbolId)
    .sort((a, b) => a.edgeId.localeCompare(b.edgeId));
}

export function edgesWithin(graph: GraphSnapshot, visible: GraphNode[]): GraphEdge[] {
  const ids = new Set(visible.map(n => n.symbolId));
  return graph.edges.filter(e => ids.has(e.fromSymbolId) && ids.has(e.toSymbolId));
}

function escapeMermaid(label: string): string {
  return label.replaceAll('\\', '\\\\').replaceAll('"', '\\"')
    .replaceAll('\n', ' ').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}

export function exportMermaid(graph: GraphSnapshot, visible: GraphNode[]): string {
  const sorted = [...visible].sort((a, b) => a.symbolId.localeCompare(b.symbolId));
  const mermaidIds = new Map(sorted.map((n, i) => [n.symbolId, 'n' + i]));
  const lines = ['flowchart TD'];
  for (const node of sorted) {
    lines.push('    ' + mermaidIds.get(node.symbolId) + '["' + escapeMermaid(node.label) + '"]');
  }
  for (const edge of edgesWithin(graph, visible)) {
    lines.push('    ' + mermaidIds.get(edge.fromSymbolId) + ' -->|"' +
      escapeMermaid(edge.relation) + '"| ' + mermaidIds.get(edge.toSymbolId));
  }
  lines.push('%% Node identity mapping (exact ArchPulse GraphRef):');
  for (const node of sorted) {
    lines.push('%% ' + mermaidIds.get(node.symbolId) + ' = ' + node.symbolId.replaceAll('\n', ' '));
  }
  return lines.join('\n');
}
