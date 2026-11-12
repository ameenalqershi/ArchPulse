// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { breadcrumb, childrenOf, edgesWithin, exportMermaid, relatedEdges, rootNode } from './graph';
import type { GraphSnapshot } from './graph';

const evidence = {
  revision: 'sha123', relativeFile: 'A.cs', line: 5, column: 2, kind: 'Static' as const
};
const graph: GraphSnapshot = {
  project: 'Demo',
  revision: 'sha123',
  nodes: [
    { symbolId: 'solution:Demo', kind: 'Solution', label: 'Demo', parentSymbolId: null, evidence },
    { symbolId: 'project:Core', kind: 'Project', label: 'Core', parentSymbolId: 'solution:Demo', evidence },
    { symbolId: 'project:Api', kind: 'Project', label: 'Api', parentSymbolId: 'solution:Demo', evidence },
    { symbolId: 'Api::M:Run', kind: 'Method', label: 'Run()', parentSymbolId: 'project:Api', evidence }
  ],
  edges: [
    { edgeId: 'e1', fromSymbolId: 'project:Api', toSymbolId: 'project:Core',
      relation: 'DependsOn', evidence }
  ],
  diagnostics: []
};

describe('real graph presentation helpers', () => {
  it('uses canonical hierarchy rather than inferred call parents', () => {
    expect(rootNode(graph)?.symbolId).toBe('solution:Demo');
    expect(childrenOf(graph, 'solution:Demo').map(n => n.symbolId))
      .toEqual(['project:Api', 'project:Core']);
    expect(breadcrumb(graph, 'Api::M:Run').map(n => n.symbolId))
      .toEqual(['solution:Demo', 'project:Api', 'Api::M:Run']);
  });
  it('keeps exact edge IDs and evidence references', () => {
    expect(relatedEdges(graph, 'project:Api')[0]?.edgeId).toBe('e1');
    expect(edgesWithin(graph, childrenOf(graph, 'solution:Demo'))).toHaveLength(1);
    expect(edgesWithin(graph, childrenOf(graph, 'project:Api'))).toHaveLength(0);
  });
  it('emits a Mermaid diagram and an exact ID map without fabricating edges', () => {
    const diagram = exportMermaid(graph, childrenOf(graph, 'solution:Demo'));
    expect(diagram).toContain('flowchart TD');
    expect(diagram).toContain('DependsOn');
    expect(diagram).toContain('project:Core');
    expect(diagram).not.toContain('Calls');
  });
  it('escapes untrusted labels in Mermaid exports', () => {
    const changed = { ...graph, nodes: graph.nodes.map(n =>
      n.symbolId === 'project:Core' ? { ...n, label: 'A" <unsafe>' } : n
    ) };
    const out = exportMermaid(changed, childrenOf(changed, 'solution:Demo'));
    expect(out).toContain('A\\" &lt;unsafe&gt;');
  });
});
