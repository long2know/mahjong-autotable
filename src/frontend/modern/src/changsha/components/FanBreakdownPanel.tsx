import {
  Card,
  CardHeader,
  Text,
  Button,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
} from '@fluentui/react-components';
import type { ChangshaGameState, WinPattern } from '../types';

const PATTERN_LABELS: Record<WinPattern, string> = {
  fullFlush: '清一色 — Flush (Big Win)',
  allPungs: '碰碰胡 — All Pungs',
  sevenPairs: '七对子 — Seven Pairs',
  standard: '标准胡 — Standard Win',
};

const WIN_TYPE_LABELS: Record<string, string> = {
  selfDraw: '自摸 — Self-Draw',
  discard: '点炮 — Discard Win',
  robbingKong: '抢杠 — Robbing Kong',
};

interface FanBreakdownPanelProps {
  state: ChangshaGameState;
  onContinue: () => void;
}

export function FanBreakdownPanel({ state, onContinue }: FanBreakdownPanelProps) {
  if (state.phase !== 'scoring' || !state.lastWin) return null;
  const win = state.lastWin;
  const score = state.lastScore;
  const winnerNick =
    state.seats.find((s) => s.index === win.winningSeatIndex)?.nick ?? `Seat ${win.winningSeatIndex}`;
  const patternDisplay = PATTERN_LABELS[win.winPattern] ?? win.winPattern;
  const winTypeDisplay = WIN_TYPE_LABELS[win.winType] ?? win.winType;

  const seatNick = (si: number) =>
    state.seats.find((s) => s.index === si)?.nick ?? `Seat ${si}`;

  return (
    <Card style={{ padding: 16, maxWidth: 480, margin: '0 auto' }}>
      <CardHeader
        header={
          <Text size={500} weight="bold">
            🏆 {winnerNick} Wins!
          </Text>
        }
      />
      <Text size={400} weight="semibold" block style={{ margin: '8px 0' }}>
        {patternDisplay}
      </Text>
      <Text size={300} block>
        {winTypeDisplay}
        {score ? ` · ${score.category === 'bigWin' ? 'Big Win' : 'Small Win'} (${score.basePoints} pts)` : ''}
      </Text>
      {score && (
        <Table size="small" style={{ marginTop: 12 }}>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>From</TableHeaderCell>
              <TableHeaderCell>To</TableHeaderCell>
              <TableHeaderCell>Amount</TableHeaderCell>
              <TableHeaderCell>Reason</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {score.payments.map((p, i) => (
              <TableRow key={i}>
                <TableCell>{seatNick(p.fromSeatIndex)}</TableCell>
                <TableCell>{seatNick(p.toSeatIndex)}</TableCell>
                <TableCell>{p.amount}</TableCell>
                <TableCell>{p.reason}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
      <div style={{ marginTop: 16, textAlign: 'center' }}>
        <Button appearance="primary" onClick={onContinue}>
          Continue
        </Button>
      </div>
    </Card>
  );
}
