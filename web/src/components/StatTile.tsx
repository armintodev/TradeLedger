import type { ReactNode } from 'react';
import { Card, Group, Text, Tooltip } from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';

interface StatTileProps {
  label: string;
  value: ReactNode;
  hint?: string;
  sub?: ReactNode;
}

export function StatTile({ label, value, hint, sub }: StatTileProps) {
  return (
    <Card padding="md" h="100%">
      <Group gap={4} wrap="nowrap">
        <Text size="xs" c="dimmed" tt="uppercase" fw={600} lh={1.4}>
          {label}
        </Text>
        {hint && (
          <Tooltip label={hint} withArrow multiline w={260}>
            <IconInfoCircle size={13} opacity={0.5} />
          </Tooltip>
        )}
      </Group>

      <Text size="xl" fw={600} mt={6} ff="monospace" style={{ lineHeight: 1.2 }}>
        {value}
      </Text>

      {sub && (
        <Text size="xs" c="dimmed" mt={4}>
          {sub}
        </Text>
      )}
    </Card>
  );
}
