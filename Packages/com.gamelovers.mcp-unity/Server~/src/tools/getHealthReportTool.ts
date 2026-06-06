import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'get_health_report';
const toolDescription = 'Collects a compact Unity health report: scene, compile state, console errors, dirty scenes, tests, and core build settings.';
const paramsSchema = z.object({
  includeTests: z.boolean().optional().default(true).describe('Whether to include Unity Test Runner test discovery. Defaults to true.'),
  testMode: z.string().optional().default('').describe('Optional test mode filter for test discovery: EditMode or PlayMode. Empty means all.'),
  maxConsoleErrors: z.number().int().min(0).max(200).optional().default(20).describe('Maximum number of console errors to include. Defaults to 20.'),
  maxTests: z.number().int().min(0).max(500).optional().default(50).describe('Maximum number of discovered tests to include. Defaults to 50.')
});

export function registerGetHealthReportTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: z.infer<typeof paramsSchema>) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(
  mcpUnity: McpUnity,
  params: z.infer<typeof paramsSchema>
): Promise<CallToolResult> {
  const {
    includeTests = true,
    testMode = '',
    maxConsoleErrors = 20,
    maxTests = 50
  } = params;

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      includeTests,
      testMode,
      maxConsoleErrors,
      maxTests
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to collect Unity health report'
    );
  }

  return {
    content: [
      {
        type: 'text',
        text: formatHealthSummary(response)
      },
      {
        type: 'text',
        text: JSON.stringify(response, null, 2)
      }
    ]
  };
}

function formatHealthSummary(report: any): string {
  const activeScene = report.scene?.activeScene;
  const compile = report.compileState || {};
  const consoleErrors = report.consoleErrors || {};
  const tests = report.tests || {};
  const buildSettings = report.buildSettings || {};

  const lines = [
    report.message || `Health report: ${report.overallStatus || 'unknown'}`,
    `Scene: ${activeScene?.name || '(none)'}${activeScene?.isDirty ? ' (dirty)' : ''}`,
    `Compile: compiling=${Boolean(compile.isCompiling)}, updating=${Boolean(compile.isUpdating)}, failed=${Boolean(compile.scriptCompilationFailed)}`,
    `Console: ${consoleErrors.errorCount ?? 0} error(s), ${consoleErrors.warningCount ?? 0} warning(s)`,
    `Dirty scenes: ${report.scene?.dirtySceneCount ?? 0}`,
    tests.included === false
      ? 'Tests: skipped'
      : `Tests: ${tests.availableCount ?? 0} discovered`,
    `Build: ${buildSettings.activeBuildTarget || '(unknown)'} / ${buildSettings.activeBuildTargetGroup || '(unknown)'}, ${buildSettings.enabledBuildSceneCount ?? 0}/${buildSettings.buildSceneCount ?? 0} scenes enabled`
  ];

  return lines.join('\n');
}
