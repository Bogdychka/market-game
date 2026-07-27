import { describe, expect, it, jest } from '@jest/globals';
import { buildPlayerAgentResult, registerPlayerAgentTool } from '../tools/playerAgentTool.js';

describe('Player Agent Tool', () => {
  it('registers the player_agent tool', () => {
    const tool = jest.fn();
    const server = { tool };
    const logger = { info: jest.fn(), debug: jest.fn(), warn: jest.fn(), error: jest.fn() };

    registerPlayerAgentTool(server as any, { sendRequest: jest.fn() } as any, logger as any);

    expect(tool).toHaveBeenCalledWith(
      'player_agent',
      expect.any(String),
      expect.objectContaining({ moveY: expect.any(Object), yawDegrees: expect.any(Object) }),
      expect.any(Function)
    );
  });

  it('returns telemetry and a PNG image content item', () => {
    const result = buildPlayerAgentResult({
      scene: 'Market',
      playerPath: 'Player',
      position: { x: 1, y: 2, z: 3 },
      viewEuler: { x: 0, y: 90, z: 0 },
      grounded: true,
      interactionPrompt: 'Open stall',
      interactionType: 'Market.Market.MarketStall',
      imagePath: 'C:/project/Artifacts/PlayerAgent/latest.png'
    }, new Uint8Array([137, 80, 78, 71]));

    expect(result.content).toHaveLength(2);
    expect(result.content[0]).toMatchObject({ type: 'text' });
    expect(result.content[1]).toEqual({
      type: 'image',
      data: 'iVBORw==',
      mimeType: 'image/png'
    });
    expect(result.data).toMatchObject({
      scene: 'Market',
      interactionPrompt: 'Open stall',
      grounded: true
    });
  });
});
