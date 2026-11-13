// SPDX-License-Identifier: Apache-2.0
import type { GraphEdge, GraphSnapshot, SourceEvidence } from './graph';

const boundaries = ['AnnotatedSink', 'OpaqueCall', 'UnsupportedSyntax',
  'DeclaredSanitizer', 'AnalysisLimit'] as const;
type Boundary = typeof boundaries[number];

export interface JourneyHop {
  kind: 'Argument' | 'Return';
  call: GraphEdge;
}
export interface JourneyWitness {
  sourceMethodSymbolId: string;
  source: SourceEvidence;
  hops: JourneyHop[];
  boundary: Boundary;
  destination: SourceEvidence;
}
export interface JourneySelection {
  status: 'NotIndexed' | 'NoWitness' | 'Mapped';
  truncated: boolean;
  findings: JourneyWitness[];
}

export function direction(hop: JourneyHop): [string, string] {
  const { fromSymbolId, toSymbolId } = hop.call;
  return hop.kind === 'Argument' ? [fromSymbolId, toSymbolId] : [toSymbolId, fromSymbolId];
}

type Plain = Record<string, unknown>;
const object = (x: unknown): x is Plain => !!x && typeof x === 'object' && !Array.isArray(x);
const text = (x: unknown): x is string => typeof x === 'string' && x.length > 0;
const location = (x: unknown, revision: string): x is SourceEvidence =>
  object(x) && x.kind === 'Static' && x.revision === revision &&
  text(x.relativeFile) && Number.isInteger(x.line) && Number(x.line) > 0 &&
  Number.isInteger(x.column) && Number(x.column) > 0;

export function parseJourneySelection(
  payload: unknown, graph: GraphSnapshot, selectedId: string
): JourneySelection {
  const bad = () => new Error('DataJourney evidence does not match the selected static graph.');
  if (!object(payload) || payload.schema !== 'archpulse.datajourney-method-selection/v1' ||
      payload.project !== graph.project || payload.revision !== graph.revision ||
      payload.symbolId !== selectedId || payload.evidence !== 'Static' ||
      payload.runtimeStatus !== 'Unknown' || typeof payload.truncated !== 'boolean' ||
      !(payload.status === 'NotIndexed' || payload.status === 'NoWitness' || payload.status === 'Mapped') ||
      !Array.isArray(payload.findings) || payload.findings.length > 512 ||
      (payload.status === 'Mapped') !== (payload.findings.length > 0) ||
      (payload.status === 'NotIndexed' && payload.truncated)) throw bad();

  const methods = new Set(graph.nodes.filter(node => node.kind === 'Method').map(node => node.symbolId));
  const edges = new Map(graph.edges.map(edge => [edge.edgeId, edge]));
  for (const item of payload.findings as unknown[]) {
    if (!object(item) || !text(item.sourceMethodSymbolId) || !methods.has(item.sourceMethodSymbolId) ||
        !location(item.source, graph.revision) || !location(item.destination, graph.revision) ||
        !boundaries.some(b => b === item.boundary) ||
        !Array.isArray(item.hops) || item.hops.length > 16) throw bad();

    let current = item.sourceMethodSymbolId;
    let matches = current === selectedId;
    for (const hop of item.hops as unknown[]) {
      if (!object(hop) || !(hop.kind === 'Argument' || hop.kind === 'Return') ||
          !object(hop.call) || !text(hop.call.edgeId) || !location(hop.call.evidence, graph.revision))
        throw bad();
      const edge = edges.get(hop.call.edgeId);
      if (!edge || edge.relation !== 'Calls' || hop.call.relation !== edge.relation ||
          hop.call.fromSymbolId !== edge.fromSymbolId || hop.call.toSymbolId !== edge.toSymbolId ||
          hop.call.evidence.relativeFile !== edge.evidence.relativeFile ||
          hop.call.evidence.line !== edge.evidence.line || hop.call.evidence.column !== edge.evidence.column)
        throw bad();

      const [from, to] = direction({ kind: hop.kind, call: edge });
      if (from !== current || !methods.has(to)) throw bad();
      current = to;
      matches ||= current === selectedId;
    }
    if (!matches) throw bad();
  }
  return payload as unknown as JourneySelection;
}
