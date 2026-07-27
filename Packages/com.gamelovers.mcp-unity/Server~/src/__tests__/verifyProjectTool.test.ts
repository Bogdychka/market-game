import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import {
  buildVerificationReport,
  compactVerificationReport,
  registerVerifyProjectTools
} from '../tools/verifyProjectTool.js';

const mockServerTool = jest.fn();
const mockServer = { tool: mockServerTool };
const mockLogger = {
  info: jest.fn(),
  debug: jest.fn(),
  warn: jest.fn(),
  error: jest.fn()
};

describe('Verify Project Tool', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('registers compact verification and issue-detail tools', () => {
    registerVerifyProjectTools(
      mockServer as any,
      { sendRequest: jest.fn() } as any,
      mockLogger as any
    );

    expect(mockServerTool.mock.calls[0][0]).toBe('verify_project');
    expect(mockServerTool.mock.calls[1][0]).toBe('get_verification_issue');
    expect(mockServerTool.mock.calls[0][2]).toHaveProperty('maxIssues');
  });

  it('builds a green compact report without raw stage payloads', () => {
    const report = buildVerificationReport(greenStages(), true);
    const compact = compactVerificationReport(report, 5);

    expect(compact).toMatchObject({
      status: 'GREEN',
      compile: 'ok',
      health: 'ok',
      tests: '2/2',
      scanner: '0E/0W/1I',
      scene: 'Market:clean',
      issues: [],
      issueCount: 0
    });
    expect(compact).not.toHaveProperty('stages');
  });

  it('returns capped issue summaries while retaining full local details', () => {
    const stages = greenStages();
    stages.scanner = {
      success: true,
      errors: 2,
      warnings: 1,
      info: 0,
      checkedAssets: 20,
      issues: [
        scannerIssue('Error', 'Broken item', 'Assets/_Project/A.asset'),
        scannerIssue('Error', 'Broken crop', 'Assets/_Project/B.asset'),
        scannerIssue('Warning', 'Legacy data', 'Assets/_Project/C.asset')
      ]
    };

    const report = buildVerificationReport(stages, true);
    const compact = compactVerificationReport(report, 2);

    expect(report.status).toBe('RED');
    expect(report.issues).toHaveLength(3);
    expect(compact.issues).toHaveLength(2);
    expect(compact.issueCount).toBe(3);
    expect(compact.issues[0]).toEqual({
      id: 'PH-001',
      severity: 'Error',
      title: 'Broken item',
      path: 'Assets/_Project/A.asset'
    });
  });
});

function greenStages(): Record<string, any> {
  return {
    compile: {
      success: true,
      message: 'Successfully recompiled all scripts with 0 warning(s)',
      logs: []
    },
    health: {
      success: true,
      overallStatus: 'ok',
      scene: { activeScene: { name: 'Market', isDirty: false } },
      consoleErrors: { errors: [] }
    },
    scanner: {
      success: true,
      errors: 0,
      warnings: 0,
      info: 1,
      checkedAssets: 139,
      issues: [{ severity: 'Info', title: 'Legacy ID' }]
    },
    tests: {
      success: true,
      passCount: 2,
      failCount: 0,
      skipCount: 0,
      results: []
    },
    scene: {
      success: true,
      activeScene: { name: 'Market', isDirty: false },
      loadedScenes: [{ name: 'Market', isDirty: false }]
    }
  };
}

function scannerIssue(severity: string, title: string, assetPath: string) {
  return {
    severity,
    title,
    description: `${title} detail`,
    assetPath,
    line: 0
  };
}
