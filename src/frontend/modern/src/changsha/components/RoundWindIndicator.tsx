import { Text } from '@fluentui/react-components';
import type { ChangshaGameState } from '../types';
import { windLabel } from '../tileUtils';

interface RoundWindIndicatorProps {
  state: ChangshaGameState;
}

export function RoundWindIndicator({ state }: RoundWindIndicatorProps) {
  const roundWind = windLabel(state.prevalentWind);

  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
      <Text
        size={600}
        weight="bold"
        style={{
          width: 40,
          height: 40,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          borderRadius: '50%',
          backgroundColor: '#e8f0fe',
          color: '#1a73e8',
        }}
      >
        {roundWind}
      </Text>
      <div>
        <Text weight="semibold" block>
          {roundWind} Round (Round {state.currentRound})
        </Text>
        <Text size={200} block>
          Hand {state.currentHand} of 4 in {roundWind} round
        </Text>
      </div>
    </div>
  );
}
