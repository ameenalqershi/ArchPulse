// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { parseMethodTestSelection } from './test-links';

const row = {
  schema: 'archpulse.method-test-selection/v1',
  project: 'Billing', revision: 'rev', targetSymbolId: 'Sut::M:Apply',
  mappingStatus: 'MappedDirectStatic',
  mappingScope: 'RoslynStaticDirectOnly', runnerDiscovery: 'NotRunnerDiscovered',
  executionStatus: 'Unknown', observedCoverage: false,
  links: [{
    testMethodSymbolId: 'Tests::M:Applies', targetSymbolId: 'Sut::M:Apply',
    testProject: 'Tests', framework: 'xUnit',
    relation: 'DirectInvocation', verification: 'StaticSourceOnly',
    executionEvidence: 'Unobserved', lastResult: 'Unknown',
    callSites: [{ revision: 'rev', relativeFile: 'Tests.cs',
      kind: 'Static', line: 5, column: 9 }]
  }]
};

describe('source-only test associations', () => {
  it('retains source evidence, not runtime claims', () => {
    const data = parseMethodTestSelection(row, 'Billing', 'rev', 'Sut::M:Apply');
    expect(data.links[0].callSites[0].line).toBe(5);
    expect(data.links[0].executionEvidence).toBe('Unobserved');
    expect(data.observedCoverage).toBe(false);
  });
  it('distinguishes not indexed from indexed but unmapped', () => {
    for (const status of ['NotIndexed', 'Unmapped'] as const) {
      const data = parseMethodTestSelection({ ...row, mappingStatus: status, links: [] },
        'Billing', 'rev', 'Sut::M:Apply');
      expect(data.mappingStatus).toBe(status);
    }
  });
  it('rejects cross-revision or misdirected evidence', () => {
    expect(() => parseMethodTestSelection(row, 'Billing', 'other', 'Sut::M:Apply')).toThrow();
    expect(() => parseMethodTestSelection(row, 'Billing', 'rev', 'Other')).toThrow();
    expect(() => parseMethodTestSelection({
      ...row, links: [{ ...row.links[0], targetSymbolId: 'Other' }]
    }, 'Billing', 'rev', 'Sut::M:Apply')).toThrow();
  });
  it('never accepts a forged execution status or unsupported observed coverage', () => {
    expect(() => parseMethodTestSelection({ ...row, executionStatus: 'Passed' },
      'Billing', 'rev', 'Sut::M:Apply')).toThrow();
    expect(() => parseMethodTestSelection({ ...row, observedCoverage: true },
      'Billing', 'rev', 'Sut::M:Apply')).toThrow();
    expect(() => parseMethodTestSelection({ ...row,
      links: [{ ...row.links[0], verification: 'Verified' }]
    }, 'Billing', 'rev', 'Sut::M:Apply')).toThrow();
  });
  it('rejects contradictory mapping status and fabricated locations', () => {
    expect(() => parseMethodTestSelection({ ...row, mappingStatus: 'Unmapped' },
      'Billing', 'rev', 'Sut::M:Apply')).toThrow();
    expect(() => parseMethodTestSelection({
      ...row, links: [{ ...row.links[0],
        callSites: [{ ...row.links[0].callSites[0], line: -3 }] }]
    }, 'Billing', 'rev', 'Sut::M:Apply')).toThrow();
  });
});
