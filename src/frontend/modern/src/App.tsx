import { Body1, Button, Card, CardHeader, Text } from '@fluentui/react-components';

export function App() {
  return (
    <main className="app-shell">
      <Card>
        <CardHeader header={<Text weight="semibold">Mahjong Autotable (Modern Shell)</Text>} />
        <Body1>
          This React + Fluent UI 9 shell is optional and incremental. Changsha delivery stays autotable-first.
        </Body1>
        <Button appearance="primary" as="a" href="/api/health">
          Check backend health
        </Button>
      </Card>
    </main>
  );
}
