import { Card, CardHeader, Text } from '@fluentui/react-components';
import type { ChangshaGameState } from '../types';
import { BankerBadge } from './BankerBadge';
import { RoundWindIndicator } from './RoundWindIndicator';

interface ChangshaHudProps {
  state: ChangshaGameState;
}

export function ChangshaHud({ state }: ChangshaHudProps) {
  return (
    <Card
      style={{
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '12px 20px',
        flexWrap: 'wrap',
        gap: 16,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
        <BankerBadge state={state} />
        <RoundWindIndicator state={state} />
      </div>
      <div style={{ display: 'flex', gap: 16, alignItems: 'center' }}>
        <CardHeader header={<Text weight="semibold">Scores</Text>} />
        {state.seats.map((seat) => (
          <div
            key={seat.index}
            style={{
              textAlign: 'center',
              minWidth: 60,
              padding: '4px 8px',
              borderRadius: 6,
              backgroundColor: seat.index === state.bankerSeat ? '#fff3e0' : '#f5f5f5',
            }}
          >
            <Text size={200} block>
              {seat.nick}
            </Text>
            <Text weight="semibold" block>
              {seat.score}
            </Text>
          </div>
        ))}
      </div>
    </Card>
  );
}
