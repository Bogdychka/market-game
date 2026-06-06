import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { registerRunTestsTool } from '../tools/runTestsTool.js';

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

describe('Run Tests Tool', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('registers timeoutSeconds in the tool schema', () => {
    registerRunTestsTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

    const [, , schema] = mockServerTool.mock.calls[0];
    expect(schema).toHaveProperty('timeoutSeconds');
  });

  it('passes timeoutSeconds to Unity and extends the request timeout', async () => {
    registerRunTestsTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
    const toolHandler = mockServerTool.mock.calls[0][3];

    mockSendRequest.mockResolvedValue({
      success: true,
      message: 'PlayMode test run completed: 0/0 passed - 0/0 failed - 0/0 skipped',
      results: [],
      testCount: 0,
      passCount: 0,
      failCount: 0,
      skipCount: 0
    });

    await toolHandler({
      testMode: 'PlayMode',
      returnOnlyFailures: false,
      returnWithLogs: true,
      timeoutSeconds: 120
    });

    expect(mockSendRequest).toHaveBeenCalledWith({
      method: 'run_tests',
      params: {
        testMode: 'PlayMode',
        testFilter: '',
        returnOnlyFailures: false,
        returnWithLogs: true,
        timeoutSeconds: 120
      }
    }, {
      timeout: 125000
    });
  });
});
