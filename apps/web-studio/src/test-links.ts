// SPDX-License-Identifier: Apache-2.0
// In-place source evidence only. These types NEVER imply executed tests/coverage.
export interface StaticCallSite {
  revision: string;
  relativeFile: string;
  line: number;
  column: number;
  kind: 'Static';
}
export interface DirectTestLink {
  testMethodSymbolId: string;
  targetSymbolId: string;
  testProject: string;
  framework: 'xUnit' | 'NUnit' | 'MSTest';
  relation: 'DirectInvocation';
  verification: 'StaticSourceOnly';
  executionEvidence: 'Unobserved';
  lastResult: 'Unknown';
  callSites: StaticCallSite[];
}
export interface MethodTestSelection {
  schema: 'archpulse.method-test-selection/v1';
  project: string;
  revision: string;
  targetSymbolId: string;
  mappingStatus: 'NotIndexed' | 'Unmapped' | 'MappedDirectStatic';
  mappingScope: 'RoslynStaticDirectOnly';
  runnerDiscovery: 'NotRunnerDiscovered';
  executionStatus: 'Unknown';
  observedCoverage: false;
  links: DirectTestLink[];
}

type Plain = Record<string, unknown>;
const object = (value: unknown): value is Plain =>
  value !== null && typeof value === 'object' && !Array.isArray(value);
const string = (value: unknown): value is string => typeof value === 'string';
export function parseMethodTestSelection(
  payload: unknown, project: string, revision: string, targetId: string
): MethodTestSelection {
  if (!object(payload) || payload.schema !== 'archpulse.method-test-selection/v1' ||
      payload.project !== project || payload.revision !== revision ||
      payload.targetSymbolId !== targetId ||
      !['NotIndexed', 'Unmapped', 'MappedDirectStatic'].includes(String(payload.mappingStatus)) ||
      payload.mappingScope !== 'RoslynStaticDirectOnly' ||
      payload.runnerDiscovery !== 'NotRunnerDiscovered' ||
      payload.executionStatus !== 'Unknown' ||
      payload.observedCoverage !== false ||
      !Array.isArray(payload.links) || payload.links.length > 50000) {
    throw new Error('Test mapping evidence has an unexpected revision or unsafe shape.');
  }
  const links = payload.links as unknown[];
  if ((payload.mappingStatus === 'MappedDirectStatic') !== (links.length > 0))
    throw new Error('Test mapping status contradicts the supplied evidence.');
  for (const raw of links) {
    if (!object(raw) || !string(raw.testMethodSymbolId) ||
        !string(raw.targetSymbolId) || raw.targetSymbolId !== targetId ||
        !string(raw.testProject) ||
        !['xUnit', 'NUnit', 'MSTest'].includes(String(raw.framework)) ||
        raw.relation !== 'DirectInvocation' || raw.verification !== 'StaticSourceOnly' ||
        raw.executionEvidence !== 'Unobserved' || raw.lastResult !== 'Unknown' ||
        !Array.isArray(raw.callSites) ||
        raw.callSites.length < 1 || raw.callSites.length > 1000 ||
        !raw.callSites.every((site: unknown) => object(site) &&
          site.kind === 'Static' && site.revision === revision &&
          string(site.relativeFile) && Number.isInteger(site.line) && Number(site.line) > 0 &&
          Number.isInteger(site.column) && Number(site.column) > 0)) {
      throw new Error('Test mapping contains an unverified or malformed association.');
    }
  }
  return payload as unknown as MethodTestSelection;
}
