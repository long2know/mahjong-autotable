import { Badge } from '@fluentui/react-components';
import type { ChangshaGameState } from '../types';
import { windLabel, windEnglish } from '../tileUtils';

interface BankerBadgeProps {
  state: ChangshaGameState;
}

export function BankerBadge({ state }: BankerBadgeProps) {
  const banker = state.seats.find((s) => s.index === state.bankerSeat);
  if (!banker) return null;
  const wind = banker.seatWind;

  return (
    <Badge
      appearance="filled"
      color="important"
      size="large"
      style={{ padding: '4px 12px', fontSize: 14 }}
    >
      🀄 Banker: {banker.nick} ({windLabel(wind)} / {windEnglish(wind)})
    </Badge>
  );
}
