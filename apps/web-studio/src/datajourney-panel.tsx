// SPDX-License-Identifier: Apache-2.0
import { useEffect, useState } from 'react';
import { direction, parseJourneySelection } from './datajourney';
import type { JourneySelection, JourneyWitness } from './datajourney';
import type { GraphSnapshot, SourceEvidence } from './graph';

function formatSite(site: SourceEvidence) {
  return `${site.relativeFile}:${site.line}:${site.column}`;
}

const boundaryLabels: Record<JourneyWitness['boundary'], string> = {
  AnnotatedSink: 'Declared sink',
  OpaqueCall: 'Opaque call',
  UnsupportedSyntax: 'Unsupported syntax',
  DeclaredSanitizer: 'Sanitizer requires review',
  AnalysisLimit: 'Analysis limit'
};

export function DataJourneyPanel({ graph, symbolId, onSelect }: {
  graph: GraphSnapshot;
  symbolId: string;
  onSelect: (id: string) => void;
}) {
  const [result, setResult] = useState<JourneySelection | 'error' | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    async function load() {
      const response = await fetch('/api/graphs/' + encodeURIComponent(graph.project) + '/' +
        encodeURIComponent(graph.revision) + '/datajourney-cross-project?symbolId=' +
        encodeURIComponent(symbolId), { signal: controller.signal, cache: 'no-store' });
      if (!response.ok) throw new Error('DataJourney request failed.');
      const parsed = parseJourneySelection(await response.json(), graph, symbolId);
      if (!controller.signal.aborted) setResult(parsed);
    }
    void load().catch(() => {
      if (!controller.signal.aborted) setResult('error');
    });
    return () => controller.abort();
  }, [graph, symbolId]);

  return <section className="method-tests datajourney" aria-label="DataJourney">
    <h4>DataJourney</h4>
    <p className="muted small">Static possible · Runtime evidence unknown</p>
    {!result && <p role="status">Loading DataJourney…</p>}
    {result === 'error' && <p role="status">DataJourney evidence is unavailable for this selection.</p>}
    {result && result !== 'error' && <>
      {result.status === 'NotIndexed' && <p>DataJourney has not been indexed for this revision.</p>}
      {result.status === 'NoWitness' && <p>No witness was found for this method in the supported analysis. This is not proof of safety.</p>}
      {result.status === 'Indeterminate' && <p role="status" className="test-unavailable">No retained witness for this method; the analysis limit prevents any conclusion about missing paths.</p>}
      {result.status === 'Mapped' && result.truncated && <p className="test-unavailable">Analysis limit reached. Some paths may be missing.</p>}
      {result.findings.map((flow, index) => <details key={index}>
        <summary>{boundaryLabels[flow.boundary]} · {flow.hops.length} {flow.hops.length === 1 ? 'hop' : 'hops'}</summary>
        <ol>
          <li>
            <button type="button" className="journey-method mono wrap" onClick={() => onSelect(flow.sourceMethodSymbolId)}>
              {flow.sourceMethodSymbolId}
            </button>
            <span className="mono wrap muted small">{formatSite(flow.source)}</span>
          </li>
          {flow.hops.map((hop, hopIndex) => {
            const [, to] = direction(hop);
            return <li key={hopIndex}>
              <strong>{hop.kind === 'Argument' ? 'Argument to' : 'Return to'}</strong>
              <button type="button" className="journey-method mono wrap" onClick={() => onSelect(to)}>{to}</button>
              <span className="mono wrap muted small">{formatSite(hop.call.evidence)}</span>
            </li>;
          })}
          <li><strong>{boundaryLabels[flow.boundary]}</strong>
            <span className="mono wrap muted small">{formatSite(flow.destination)}</span></li>
        </ol>
      </details>)}
    </>}
  </section>;
}
