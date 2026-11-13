// SPDX-License-Identifier: Apache-2.0
import { useEffect, useState } from 'react';
import { parseMethodTestSelection } from './test-links';
import type { MethodTestSelection } from './test-links';

type LoadState =
  | { status: 'loading' }
  | { status: 'error'; error: string }
  | { status: 'loaded'; result: MethodTestSelection };

// An inspector is mounted under an exact selection key by Studio. Keeping its
// asynchronous state here prevents an old method's result from ever appearing
// under the next method while React schedules effects and network requests.
export function MethodTestsPanel({ project, revision, symbolId }: {
  project: string;
  revision: string;
  symbolId: string;
}) {
  const [state, setState] = useState<LoadState>({ status: 'loading' });

  useEffect(() => {
    const controller = new AbortController();
    async function load() {
      const response = await fetch('/api/graphs/' +
        encodeURIComponent(project) + '/' + encodeURIComponent(revision) +
        '/test-links?symbolId=' + encodeURIComponent(symbolId),
        { signal: controller.signal, cache: 'no-store' });
      if (!response.ok) {
        throw new Error(response.status === 404
          ? 'Exact test mapping or method is unavailable.'
          : 'Test mapping unavailable (HTTP ' + response.status + ').');
      }
      const payload: unknown = await response.json();
      const result = parseMethodTestSelection(payload, project, revision, symbolId);
      if (!controller.signal.aborted) setState({ status: 'loaded', result });
    }

    void load().catch((error: unknown) => {
      if (!controller.signal.aborted) setState({
        status: 'error',
        error: error instanceof Error ? error.message : 'Test mapping unavailable.'
      });
    });
    return () => controller.abort();
  }, [project, revision, symbolId]);

  return <section className="method-tests" aria-label="Method tests">
    <h4>Tests · static source evidence</h4>
    {state.status === 'loading' &&
      <p className="muted" role="status">Loading source-based test associations…</p>}
    {state.status === 'error' &&
      <p className="test-unavailable" role="status">{state.error} No result is inferred.</p>}
    {state.status === 'loaded' && state.result.mappingStatus === 'NotIndexed' &&
      <p className="test-unavailable">Test links not indexed for this revision.
        Index the trusted solution with the CLI and save the sidecar.</p>}
    {state.status === 'loaded' && state.result.mappingStatus === 'Unmapped' &&
      <p className="muted">No direct static test links mapped. This does not mean zero coverage.</p>}
    {state.status === 'loaded' && state.result.mappingStatus === 'MappedDirectStatic' && <>
      <div className="test-summary">{state.result.links.length} direct static test link{state.result.links.length === 1 ? '' : 's'}</div>
      <div className="test-links-list">{state.result.links.map(link =>
        <article className="test-link" key={link.testMethodSymbolId + ':' + link.targetSymbolId}>
          <strong className="mono wrap">{link.testMethodSymbolId}</strong>
          <span className="muted small">{link.framework} · DirectInvocation · Static source</span>
          <span className="test-unavailable small">Not run · Unobserved · Runner case unverified</span>
          <ul>{link.callSites.map(site =>
            <li className="mono wrap" key={site.relativeFile + ':' + site.line + ':' + site.column}>
              {site.relativeFile}:{site.line}:{site.column} · {site.kind}
            </li>)}</ul>
        </article>)}</div>
    </>}
    <p className="muted small">Test declarations are not runner-discovered test cases. No observed calls, coverage,
      test execution, or pass/fail status is available from these links.</p>
  </section>;
}
