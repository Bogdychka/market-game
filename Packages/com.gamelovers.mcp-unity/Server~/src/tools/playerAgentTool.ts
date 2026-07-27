import { readFile } from 'node:fs/promises';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import * as z from 'zod';
import { McpUnity } from '../unity/mcpUnity.js';
import { ErrorType, McpUnityError } from '../utils/errors.js';
import { Logger } from '../utils/logger.js';

const toolName = 'player_agent';
const toolDescription = 'Drives the live first-person player in Unity Play Mode, optionally interacts, and returns a Game View screenshot plus telemetry.';

const paramsSchema = z.object({
  moveX: z.number().min(-1).max(1).default(0).describe('Strafe input: -1 left, +1 right.'),
  moveY: z.number().min(-1).max(1).default(0).describe('Movement input: -1 backward, +1 forward.'),
  yawDegrees: z.number().min(-180).max(180).default(0).describe('Turn right by positive degrees or left by negative degrees.'),
  pitchDegrees: z.number().min(-85).max(85).default(0).describe('Look up by positive degrees or down by negative degrees.'),
  duration: z.number().min(0).max(3).default(0).describe('Movement duration in seconds. Use 0 to observe without moving.'),
  sprint: z.boolean().default(false).describe('Use the player sprint speed for this movement.'),
  jump: z.boolean().default(false).describe('Jump at the start of this movement.'),
  interact: z.boolean().default(false).describe('Use the current interactable after moving and turning.')
});

export function registerPlayerAgentTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any) => {
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

async function toolHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  const validatedParams = paramsSchema.parse(params);
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: validatedParams
  }, {
    timeout: 10000
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to control or observe the player'
    );
  }

  let png: Buffer;
  try {
    png = await readFile(response.imagePath);
  } catch (error) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      `Unity captured the Game View, but the MCP server could not read it: ${error instanceof Error ? error.message : String(error)}`
    );
  }

  return buildPlayerAgentResult(response, png);
}

export function buildPlayerAgentResult(response: any, png: Uint8Array): CallToolResult {
  const telemetry = {
    scene: response.scene,
    playerPath: response.playerPath,
    position: response.position,
    viewEuler: response.viewEuler,
    grounded: response.grounded,
    interactionPrompt: response.interactionPrompt,
    interactionType: response.interactionType,
    imagePath: response.imagePath
  };

  return {
    content: [
      {
        type: 'text',
        text: JSON.stringify(telemetry, null, 2)
      },
      {
        type: 'image',
        data: Buffer.from(png).toString('base64'),
        mimeType: 'image/png'
      }
    ],
    data: telemetry,
    isError: false
  };
}
