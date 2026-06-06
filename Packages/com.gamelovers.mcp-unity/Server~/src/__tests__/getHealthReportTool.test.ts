import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { registerGetHealthReportTool } from '../tools/getHealthReportTool.js';

const mockSendRequest = jest.fn();
const mockMcpUnity = {
  sendRequest: mockSendRequest
};

const mockLogger = {
  info: jest.fn(),
  debug: jest.fn(),
  warn: jest.fn(),
  error: jest.fn()
};

const mockServerTool = jest.fn();
const mockServer = {
  tool: mockServerTool
};

describe('Get Health Report Tool', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('registers the health report schema', () => {
    registerGetHealthReportTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

    const [, , schema] = mockServerTool.mock.calls[0];
    expect(schema).toHaveProperty('includeTests');
    expect(schema).toHaveProperty('testMode');
    expect(schema).toHaveProperty('maxConsoleErrors');
    expect(schema).toHaveProperty('maxTests');
  });

  it('passes normalized health report parameters to Unity', async () => {
    registerGetHealthReportTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    const toolHandler = mockServerTool.mock.calls[0][3];

    mockSendRequest.mockResolvedValue({
      success: true,
      message: 'Health report: ok.',
      overallStatus: 'ok',
      scene: {
        activeScene: { name: 'Bootstrap', isDirty: false },
        dirtySceneCount: 0
      },
      compileState: {
        isCompiling: false,
        isUpdating: false,
        scriptCompilationFailed: false
      },
      consoleErrors: {
        errorCount: 0,
        warningCount: 0
      },
      tests: {
        included: true,
        availableCount: 55
      },
      buildSettings: {
        activeBuildTarget: 'StandaloneWindows64',
        activeBuildTargetGroup: 'Standalone',
        enabledBuildSceneCount: 3,
        buildSceneCount: 3
      }
    });

    const result = await toolHandler({
      includeTests: true,
      testMode: 'EditMode',
      maxConsoleErrors: 10,
      maxTests: 25
    });

    expect(mockSendRequest).toHaveBeenCalledWith({
      method: 'get_health_report',
      params: {
        includeTests: true,
        testMode: 'EditMode',
        maxConsoleErrors: 10,
        maxTests: 25
      }
    });
    expect(result.content[0].text).toContain('Health report: ok.');
    expect(result.content[0].text).toContain('Tests: 55 discovered');
  });
});
