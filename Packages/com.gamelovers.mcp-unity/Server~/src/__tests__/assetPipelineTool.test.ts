import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import {
  buildAssetPipelineReport,
  compactAssetPipelineReport,
  registerAssetPipelineTools
} from '../tools/assetPipelineTool.js';

const mockServerTool = jest.fn();
const mockServer = { tool: mockServerTool };
const mockLogger = {
  info: jest.fn(),
  debug: jest.fn(),
  warn: jest.fn(),
  error: jest.fn()
};

describe('Asset Pipeline Tool', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('registers compact analysis and issue-detail tools', () => {
    registerAssetPipelineTools(
      mockServer as any,
      { sendRequest: jest.fn() } as any,
      mockLogger as any
    );

    expect(mockServerTool.mock.calls[0][0]).toBe('analyze_asset_model');
    expect(mockServerTool.mock.calls[1][0]).toBe('get_asset_pipeline_issue');
    expect(mockServerTool.mock.calls[0][2]).toHaveProperty('maxIssues');
  });

  it('returns compact metrics and caps issue summaries', () => {
    const report = buildAssetPipelineReport({
      success: true,
      status: 'WARNING',
      assetPath: 'Assets/blender/wood_box.fbx',
      profile: 'StaticProp',
      dimensions: { x: 1.25, y: 0.5, z: 0.75 },
      meshCount: 1,
      vertexCount: 24,
      triangleCount: 12,
      materialCount: 1,
      hasCollider: false,
      hasProjectPrefab: false,
      importScale: 1,
      issues: [
        { severity: 'Warning', title: 'Generic name', description: 'Rename Cube.001.' },
        { severity: 'Info', title: 'No collider', description: 'Create a wrapper.' }
      ]
    });

    const compact = compactAssetPipelineReport(report, 1);
    expect(compact).toMatchObject({
      status: 'WARNING',
      asset: 'Assets/blender/wood_box.fbx',
      profile: 'StaticProp',
      size: '1.250x0.500x0.750m',
      tris: 12,
      vertices: 24,
      issues: [{ id: 'AP-001', severity: 'Warning', title: 'Generic name' }],
      issueCount: 2,
      report: 'Artifacts/AssetPipeline/latest.json'
    });
    expect(compact).not.toHaveProperty('raw');
  });
});
