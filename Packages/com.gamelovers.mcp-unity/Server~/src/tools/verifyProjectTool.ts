import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import * as z from 'zod';
import { McpUnity } from '../unity/mcpUnity.js';
import { ErrorType, McpUnityError } from '../utils/errors.js';
import { Logger } from '../utils/logger.js';

const reportRelativePath = 'Artifacts/Verification/latest.json';
const verifySchema = z.object({
  refresh: z.boolean().optional().default(false).describe('Refresh AssetDatabase before compiling. Use after adding files.'),
  runTests: z.boolean().optional().default(true).describe('Run EditMode tests after compile, health, and Project Health checks.'),
  testFilter: z.string().optional().default('Market.Tests').describe('Unity Test Runner filter. Use a specific namespace or class for focused verification.'),
  timeoutSeconds: z.number().positive().max(900).optional().default(180).describe('Test timeout in seconds.'),
  maxIssues: z.number().int().min(0).max(10).optional().default(5).describe('Maximum brief issues returned to the caller. Full details remain on disk.')
});
const issueSchema = z.object({
  id: z.string().min(1).describe('Issue ID from verify_project, for example PH-001.')
});

export interface VerificationIssue {
  id: string;
  severity: 'Error' | 'Warning';
  source: string;
  title: string;
  detail: string;
  path?: string;
  line?: number;
}

export interface VerificationReport {
  generatedAt: string;
  status: 'GREEN' | 'YELLOW' | 'RED';
  reportPath: string;
  summary: Record<string, string | number>;
  issues: VerificationIssue[];
  stages: Record<string, any>;
}

export function registerVerifyProjectTools(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  registerVerifyProjectTool(server, mcpUnity, logger);
  registerGetVerificationIssueTool(server, logger);
}

function registerVerifyProjectTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  server.tool(
    'verify_project',
    'Runs the local Unity verification gate and returns only a compact summary. Full details are written to disk.',
    verifySchema.shape,
    async (params: z.infer<typeof verifySchema>): Promise<CallToolResult> => {
      const options = verifySchema.parse(params);
      const stages = await runStages(mcpUnity, options);
      const report = buildVerificationReport(stages, options.runTests);
      await saveReport(report);
      const compact = compactVerificationReport(report, options.maxIssues);
      logger.info('Local project verification completed', compact);
      return {
        content: [{ type: 'text', text: JSON.stringify(compact) }],
        data: compact,
        isError: false
      };
    }
  );
}

function registerGetVerificationIssueTool(server: McpServer, logger: Logger) {
  server.tool(
    'get_verification_issue',
    'Returns full local details for one issue ID from the latest verify_project report.',
    issueSchema.shape,
    async (params: z.infer<typeof issueSchema>): Promise<CallToolResult> => {
      const { id } = issueSchema.parse(params);
      const report = await loadLatestReport();
      const issue = report.issues.find(candidate => candidate.id === id);
      if (!issue) {
        throw new McpUnityError(
          ErrorType.TOOL_EXECUTION,
          `Issue '${id}' was not found in ${reportRelativePath}`
        );
      }

      const details = { generatedAt: report.generatedAt, reportPath: report.reportPath, issue };
      logger.info(`Returning verification issue ${id}`);
      return {
        content: [{ type: 'text', text: JSON.stringify(details) }],
        data: details,
        isError: false
      };
    }
  );
}

async function runStages(
  mcpUnity: McpUnity,
  options: z.infer<typeof verifySchema>
): Promise<Record<string, any>> {
  const stages: Record<string, any> = {};
  if (options.refresh) {
    stages.refresh = await requestStage(
      mcpUnity,
      'execute_menu_item',
      { menuPath: 'Assets/Refresh' },
      30000
    );
  }

  stages.compile = await requestStage(
    mcpUnity,
    'recompile_scripts',
    { returnWithLogs: true, logsLimit: 50 },
    130000
  );
  stages.health = await requestStage(
    mcpUnity,
    'get_health_report',
    { includeTests: false, testMode: '', maxConsoleErrors: 20, maxTests: 0 },
    60000
  );

  if (isCompileSuccessful(stages.compile)) {
    stages.scanner = await requestStage(mcpUnity, 'project_health_scan', {}, 60000);
    if (options.runTests) {
      stages.tests = await requestStage(
        mcpUnity,
        'run_tests',
        {
          testMode: 'EditMode',
          testFilter: options.testFilter,
          returnOnlyFailures: true,
          returnWithLogs: false,
          timeoutSeconds: options.timeoutSeconds
        },
        (options.timeoutSeconds + 10) * 1000
      );
    }
  } else {
    stages.scanner = { success: false, skipped: true, message: 'Skipped because compilation failed.' };
    if (options.runTests)
      stages.tests = { success: false, skipped: true, message: 'Skipped because compilation failed.' };
  }

  stages.scene = await requestStage(mcpUnity, 'get_scene_info', {}, 30000);
  return stages;
}

async function requestStage(
  mcpUnity: McpUnity,
  method: string,
  params: Record<string, unknown>,
  timeout: number
): Promise<any> {
  let failure = 'Unknown local verification error.';
  for (let attempt = 1; attempt <= 6; attempt++) {
    try {
      const response = await mcpUnity.sendRequest({ method, params }, { timeout });
      if (response?.success !== false || !isRetryableConnectionError(response?.message))
        return response;
      failure = response.message || failure;
    } catch (error) {
      failure = error instanceof Error ? error.message : String(error);
      if (!isRetryableConnectionError(failure))
        break;
    }

    if (attempt < 6)
      await delay(attempt * 500);
  }

  return { success: false, message: failure };
}

function isRetryableConnectionError(message: string | undefined): boolean {
  return /connection|closed|ECONNREFUSED|timed out|unknown error|not connected/i.test(message || '');
}

function delay(milliseconds: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}

export function buildVerificationReport(
  stages: Record<string, any>,
  testsRequested: boolean
): VerificationReport {
  const issues: VerificationIssue[] = [];
  collectStageFailures(stages, issues);
  collectCompileIssues(stages.compile, issues);
  collectHealthIssues(stages.health, issues);
  collectScannerIssues(stages.scanner, issues);
  if (testsRequested)
    collectTestIssues(stages.tests, issues);
  collectSceneIssues(stages.scene, issues);

  const status = issues.some(issue => issue.severity === 'Error')
    ? 'RED'
    : issues.some(issue => issue.severity === 'Warning') ? 'YELLOW' : 'GREEN';
  const reportPath = path.resolve(process.cwd(), reportRelativePath);

  return {
    generatedAt: new Date().toISOString(),
    status,
    reportPath,
    summary: buildSummary(stages, testsRequested),
    issues,
    stages
  };
}

function collectStageFailures(stages: Record<string, any>, issues: VerificationIssue[]) {
  for (const [name, stage] of Object.entries(stages)) {
    if (stage?.success !== false || stage?.skipped)
      continue;

    addIssue(issues, 'VERIFY', 'Error', name, `${name} stage failed`, stage?.message || 'Unknown local verification error.');
  }
}

function collectCompileIssues(compile: any, issues: VerificationIssue[]) {
  for (const log of compile?.logs || []) {
    if (log?.type !== 'Error' && log?.type !== 'Warning')
      continue;

    addIssue(
      issues,
      'COMPILE',
      log.type,
      'compile',
      log.message || `Compiler ${log.type.toLowerCase()}`,
      log.message || '',
      log.file,
      log.line
    );
  }

  if (!isCompileSuccessful(compile) && !issues.some(issue => issue.source === 'compile'))
    addIssue(issues, 'COMPILE', 'Error', 'compile', 'Compilation failed', compile?.message || 'Unity compilation failed.');
}

function collectHealthIssues(health: any, issues: VerificationIssue[]) {
  if (health?.success && health?.overallStatus !== 'ok')
    addIssue(issues, 'HEALTH', 'Error', 'health', 'Unity health is not OK', health.message || String(health.overallStatus));

  for (const error of health?.consoleErrors?.errors || []) {
    addIssue(
      issues,
      'HEALTH',
      'Error',
      'health',
      error.message || 'Unity console error',
      error.stackTrace || error.message || ''
    );
  }
}

function collectScannerIssues(scanner: any, issues: VerificationIssue[]) {
  for (const issue of scanner?.issues || []) {
    if (issue?.severity !== 'Error' && issue?.severity !== 'Warning')
      continue;

    addIssue(
      issues,
      'PH',
      issue.severity,
      'scanner',
      issue.title || 'Project Health issue',
      issue.description || '',
      issue.assetPath,
      issue.line
    );
  }
}

function collectTestIssues(tests: any, issues: VerificationIssue[]) {
  for (const result of tests?.results || []) {
    addIssue(
      issues,
      'TEST',
      'Error',
      'tests',
      result.fullName || result.name || 'EditMode test failed',
      result.message || result.resultState || 'Test failed.'
    );
  }

  if (tests?.success && Number(tests.failCount || 0) > 0 && !(tests.results || []).length)
    addIssue(issues, 'TEST', 'Error', 'tests', 'EditMode tests failed', tests.message || 'One or more tests failed.');
}

function collectSceneIssues(scene: any, issues: VerificationIssue[]) {
  const dirtyScenes = scene?.loadedScenes?.filter((candidate: any) => candidate?.isDirty) || [];
  for (const dirty of dirtyScenes) {
    addIssue(
      issues,
      'SCENE',
      'Error',
      'scene',
      `Scene '${dirty.name || '(unnamed)'}' is dirty`,
      'Verification must leave all loaded scenes clean.',
      dirty.path
    );
  }
}

function addIssue(
  issues: VerificationIssue[],
  prefix: string,
  severity: 'Error' | 'Warning',
  source: string,
  title: string,
  detail: string,
  issuePath?: string,
  line?: number
) {
  const index = issues.filter(issue => issue.id.startsWith(`${prefix}-`)).length + 1;
  issues.push({
    id: `${prefix}-${String(index).padStart(3, '0')}`,
    severity,
    source,
    title,
    detail,
    ...(issuePath ? { path: issuePath } : {}),
    ...(line ? { line } : {})
  });
}

function buildSummary(
  stages: Record<string, any>,
  testsRequested: boolean
): Record<string, string | number> {
  const tests = stages.tests;
  const completedTests = Number(tests?.passCount || 0) + Number(tests?.failCount || 0) + Number(tests?.skipCount || 0);
  const activeScene = stages.scene?.activeScene;
  const healthScene = stages.health?.scene?.activeScene;
  const sceneName = activeScene?.name || healthScene?.name;
  return {
    compile: isCompileSuccessful(stages.compile) ? 'ok' : 'failed',
    health: stages.health?.overallStatus || 'failed',
    tests: testsRequested
      ? `${Number(tests?.passCount || 0)}/${completedTests}`
      : 'skipped',
    scanner: stages.scanner?.success
      ? `${Number(stages.scanner.errors || 0)}E/${Number(stages.scanner.warnings || 0)}W/${Number(stages.scanner.info || 0)}I`
      : 'failed',
    checkedAssets: Number(stages.scanner?.checkedAssets || 0),
    scene: sceneName
      ? `${sceneName}:${activeScene?.isDirty ? 'dirty' : 'clean'}`
      : 'unknown'
  };
}

function isCompileSuccessful(compile: any): boolean {
  return compile?.success === true
    && !/completed with [1-9][0-9]* error/i.test(compile?.message || '')
    && !(compile?.logs || []).some((log: any) => log?.type === 'Error');
}

export function compactVerificationReport(report: VerificationReport, maxIssues: number) {
  return {
    status: report.status,
    ...report.summary,
    issues: report.issues.slice(0, maxIssues).map(issue => ({
      id: issue.id,
      severity: issue.severity,
      title: issue.title,
      ...(issue.path ? { path: issue.path } : {})
    })),
    issueCount: report.issues.length,
    report: reportRelativePath
  };
}

async function saveReport(report: VerificationReport) {
  await mkdir(path.dirname(report.reportPath), { recursive: true });
  await writeFile(report.reportPath, JSON.stringify(report, null, 2), 'utf8');
}

async function loadLatestReport(): Promise<VerificationReport> {
  const reportPath = path.resolve(process.cwd(), reportRelativePath);
  try {
    return JSON.parse(await readFile(reportPath, 'utf8')) as VerificationReport;
  } catch (error) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      `Could not read ${reportRelativePath}: ${error instanceof Error ? error.message : String(error)}`
    );
  }
}
