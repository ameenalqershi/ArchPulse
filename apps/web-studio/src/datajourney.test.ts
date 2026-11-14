// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { direction, parseJourneySelection } from './datajourney';
import type { GraphSnapshot, SourceEvidence } from './graph';

const evidence: SourceEvidence = {
  revision: 'rev', relativeFile: 'Entry.cs', line: 3, column: 7, kind: 'Static'
};
const edge = { edgeId: 'call', fromSymbolId: 'entry', toSymbolId: 'source', relation: 'Calls', evidence };
const graph: GraphSnapshot = {
  project: 'Sample', revision: 'rev', edges: [edge], diagnostics: [],
  nodes: ['entry', 'source', 'other'].map(symbolId =>
    ({ symbolId, kind: 'Method', label: symbolId, parentSymbolId: null, evidence }))
};
const response = {
  schema: 'archpulse.datajourney-method-selection/v1', project: graph.project,
  revision: graph.revision, symbolId: 'entry', evidence: 'Static', runtimeStatus: 'Unknown',
  status: 'Mapped', truncated: true,
  findings: [{
    sourceMethodSymbolId: 'source', source: evidence, boundary: 'OpaqueCall',
    destination: evidence, hops: [{ kind: 'Return' as const, call: edge }]
  }]
};

describe('DataJourney graph evidence', () => {
  it('reverses the call edge for return propagation', () => {
    const parsed = parseJourneySelection(response, graph, 'entry');
    expect(direction(parsed.findings[0].hops[0])).toEqual(['source', 'entry']);
  });
  it('rejects stale, observed, and unrelated reports', () => {
    expect(() => parseJourneySelection({ ...response, revision: 'old' }, graph, 'entry')).toThrow();
    expect(() => parseJourneySelection({ ...response, runtimeStatus: 'Observed' }, graph, 'entry')).toThrow();
    expect(() => parseJourneySelection({ ...response, symbolId: 'other' }, graph, 'other')).toThrow();
  });
  it('rejects forged edges and reversed witness order', () => {
    const forged = structuredClone(response);
    forged.findings[0].hops[0].call.evidence.line += 1;
    expect(() => parseJourneySelection(forged, graph, 'entry')).toThrow();
    const discontinuous = structuredClone(response);
    discontinuous.findings[0].sourceMethodSymbolId = 'entry';
    expect(() => parseJourneySelection(discontinuous, graph, 'entry')).toThrow();
  });
  it('distinguishes missing index and no witness', () => {
    const empty = { ...response, findings: [], truncated: false };
    expect(parseJourneySelection({ ...empty, status: 'NotIndexed' }, graph, 'entry').status).toBe('NotIndexed');
    expect(parseJourneySelection({ ...empty, status: 'NoWitness' }, graph, 'entry').status).toBe('NoWitness');
    expect(() => parseJourneySelection(empty, graph, 'entry')).toThrow();
    const limited = { ...empty, truncated: true };
    expect(parseJourneySelection({ ...limited, status: 'Indeterminate' },
      graph, 'entry').status).toBe('Indeterminate');
    expect(() => parseJourneySelection({ ...limited, status: 'NoWitness' }, graph, 'entry')).toThrow();
    expect(() => parseJourneySelection({ ...empty, status: 'Indeterminate' }, graph, 'entry')).toThrow();
  });
});
