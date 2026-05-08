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
import type { ChangshaGameState, SeatIndex } from '../types';

const PATTERN_LABELS: Record<string, string> = {
  '清一色': '清一色 — Flush (Big Win)',
  '碰碰胡': '碰碰胡 — All Pungs',
  '七对子': '七对子 — Seven Pairs',
  '自摸': '自摸 — Self-Draw',
  '点炮': '点炮 — Discard Win',
};

interface FanBreakdownPanelProps {
  state: ChangshaGameState;
  onContinue: () => void;
}

export function FanBreakdownPanel({ state, onContinue }: FanBreakdownPanelProps) {
  if (state.phase !== 'scoring' || !state.lastWin) return null;
  const win = state.lastWin;
  const winnerNick = state.seats.find((s) => s.index === win.seatIndex)?.nick ?? `Seat ${win.seatIndex}`;
  const patternDisplay = PATTERN_LABELS[win.pattern] ?? win.pattern;

  const seatNick = (si: SeatIndex) =>
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
      <Table size="small" style={{ marginTop: 12 }}>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>From</TableHeaderCell>
            <TableHeaderCell>To</TableHeaderCell>
            <TableHeaderCell>Amount</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {win.payments.map((p, i) => (
            <TableRow key={i}>
              <TableCell>{seatNick(p.from)}</TableCell>
              <TableCell>{seatNick(p.to)}</TableCell>
              <TableCell>{p.amount}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      <div style={{ marginTop: 16, textAlign: 'center' }}>
        <Button appearance="primary" onClick={onContinue}>
          Continue
        </Button>
      </div>
    </Card>
  );
}
