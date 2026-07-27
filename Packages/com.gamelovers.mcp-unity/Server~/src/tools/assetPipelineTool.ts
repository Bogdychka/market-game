import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import * as z from 'zod';
import { McpUnity } from '../unity/mcpUnity.js';
import { ErrorType, McpUnityError } from '../utils/errors.js';
import { Logger } from '../utils/logger.js';

const reportRelativePath = 'Artifacts/AssetPipeline/latest.json';
const analyzeSchema = z.object({
  assetPath: z.string().min(1).describe('Unity path to one imported FBX or OBJ model under Assets/.'),
  profile: z.enum(['StaticProp', 'FoodItem', 'Structure', 'Character']).optional().default('StaticProp'),
  maxIssues: z.number().int().min(0).max(10).optional().default(5)
    .describe('Maximum brief issues returned. Full details remain on disk.')
});
const issueSchema = z.object({
  id: z.string().min(1).describe('Issue ID returned by analyze_asset_model, for example AP-001.')
});

export interface AssetPipelineMcpIssue {
  id: string;
  severity: string;
  title: string;
  description: string;
}

export interface AssetPipelineMcpReport {
  generatedAt: string;
  reportPath: string;
  status: string;
  assetPath: string;
  profile: string;
  dimensions: { x: number; y: number; z: number };
  meshCount: number;
  vertexCount: number;
  triangleCount: number;
  materialCount: number;
  hasCollider: boolean;
  hasProjectPrefab: boolean;
  importScale: number;
  issues: AssetPipelineMcpIssue[];
  raw: any;
}

export function registerAssetPipelineTools(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  server.tool(
    'analyze_asset_model',
    'Runs local read-only FBX/OBJ analysis and returns a compact summary. Full findings are written to disk.',
    analyzeSchema.shape,
    async (params: z.infer<typeof analyzeSchema>): Promise<CallToolResult> => {
      const options = analyzeSchema.parse(params);
      const response = await mcpUnity.sendRequest({
        method: 'asset_pipeline_analyze',
        params: { assetPath: options.assetPath, profile: options.profile }
      }, { timeout: 60000 });
      if (!response.success) {
        throw new McpUnityError(
          ErrorType.TOOL_EXECUTION,
          response.message || 'Asset Pipeline analysis failed.'
        );
      }

      const report = buildAssetPipelineReport(response);
      await saveReport(report);
      const compact = compactAssetPipelineReport(report, options.maxIssues);
      logger.info('Asset Pipeline analysis completed', compact);
      return {
        content: [{ type: 'text', text: JSON.stringify(compact) }],
        data: compact,
        isError: false
      };
    }
  );

  server.tool(
    'get_asset_pipeline_issue',
    'Returns full local details for one issue ID from the latest asset model analysis.',
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

      const details = {
        generatedAt: report.generatedAt,
        assetPath: report.assetPath,
        report: reportRelativePath,
        issue
      };
      logger.info(`Returning Asset Pipeline issue ${id}`);
      return {
        content: [{ type: 'text', text: JSON.stringify(details) }],
        data: details,
        isError: false
      };
    }
  );
}

export function buildAssetPipelineReport(response: any): AssetPipelineMcpReport {
  const reportPath = path.resolve(process.cwd(), reportRelativePath);
  return {
    generatedAt: new Date().toISOString(),
    reportPath,
    status: response.status,
    assetPath: response.assetPath,
    profile: response.profile,
    dimensions: {
      x: Number(response.dimensions?.x || 0),
      y: Number(response.dimensions?.y || 0),
      z: Number(response.dimensions?.z || 0)
    },
    meshCount: Number(response.meshCount || 0),
    vertexCount: Number(response.vertexCount || 0),
    triangleCount: Number(response.triangleCount || 0),
    materialCount: Number(response.materialCount || 0),
    hasCollider: Boolean(response.hasCollider),
    hasProjectPrefab: Boolean(response.hasProjectPrefab),
    importScale: Number(response.importScale || 0),
    issues: (response.issues || []).map((issue: any, index: number) => ({
      id: `AP-${String(index + 1).padStart(3, '0')}`,
      severity: issue.severity || 'Info',
      title: issue.title || 'Asset Pipeline issue',
      description: issue.description || ''
    })),
    raw: response
  };
}

export function compactAssetPipelineReport(report: AssetPipelineMcpReport, maxIssues: number) {
  return {
    status: report.status,
    asset: report.assetPath,
    profile: report.profile,
    size: formatDimensions(report.dimensions),
    tris: report.triangleCount,
    vertices: report.vertexCount,
    meshes: report.meshCount,
    materials: report.materialCount,
    importScale: report.importScale,
    collider: report.hasCollider,
    prefab: report.hasProjectPrefab,
    issues: report.issues.slice(0, maxIssues).map(issue => ({
      id: issue.id,
      severity: issue.severity,
      title: issue.title
    })),
    issueCount: report.issues.length,
    report: reportRelativePath
  };
}

function formatDimensions(dimensions: { x: number; y: number; z: number }): string {
  return `${dimensions.x.toFixed(3)}x${dimensions.y.toFixed(3)}x${dimensions.z.toFixed(3)}m`;
}

async function saveReport(report: AssetPipelineMcpReport) {
  await mkdir(path.dirname(report.reportPath), { recursive: true });
  await writeFile(report.reportPath, JSON.stringify(report, null, 2), 'utf8');
}

async function loadLatestReport(): Promise<AssetPipelineMcpReport> {
  const reportPath = path.resolve(process.cwd(), reportRelativePath);
  try {
    return JSON.parse(await readFile(reportPath, 'utf8')) as AssetPipelineMcpReport;
  } catch (error) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      `Could not read ${reportRelativePath}: ${error instanceof Error ? error.message : String(error)}`
    );
  }
}
