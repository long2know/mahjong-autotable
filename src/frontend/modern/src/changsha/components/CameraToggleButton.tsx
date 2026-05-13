import { Button, Tooltip } from '@fluentui/react-components';

/**
 * HUD button that toggles the autotable iframe's camera between
 * perspective and orthographic modes. Mirrors upstream's `P` keybind
 * (Default #3 — both surfaces are supported simultaneously).
 *
 * The actual toggle is performed inside the iframe by
 * `changsha-bridge-receiver.js`, which synthesizes a `KeyboardEvent`
 * with key 'p' on `document` so the upstream bundle's window-level
 * keydown listener picks it up.
 *
 * Wire-level contract (postMessage envelope):
 *   { proto: 'changsha-bridge/1', type: 'camera-toggle' }
 */
export interface CameraToggleButtonProps {
  onToggle: () => void;
  disabled?: boolean;
}

export function CameraToggleButton({ onToggle, disabled }: CameraToggleButtonProps) {
  return (
    <Tooltip
      content="Toggle 3D perspective / flat view (also: press P)"
      relationship="label"
      withArrow
    >
      <Button
        appearance="secondary"
        size="small"
        onClick={onToggle}
        disabled={disabled}
        aria-label="Toggle camera perspective"
        data-testid="camera-toggle-button"
      >
        🎥 Toggle View
      </Button>
    </Tooltip>
  );
}
