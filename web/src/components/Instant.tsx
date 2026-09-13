import { Text, Tooltip } from '@mantine/core';
import { useTimeZone } from '@/lib/timeZone';
import { formatInstant } from '@/lib/time';

interface InstantProps {
  value: string | null | undefined;
  dateOnly?: boolean;
  seconds?: boolean;
  size?: string;
  c?: string;
}

/** An instant in the trader's zone, with the raw UTC value one hover away. */
export function Instant({ value, dateOnly, seconds, size = 'sm', c }: InstantProps) {
  const timeZone = useTimeZone();
  const rendered = formatInstant(value, { timeZone, dateOnly, seconds });

  if (!value) {
    return (
      <Text component="span" size={size} c="dimmed">
        {rendered}
      </Text>
    );
  }

  return (
    <Tooltip label={`${value} (${timeZone})`} withArrow openDelay={400}>
      <Text component="span" size={size} c={c} style={{ whiteSpace: 'nowrap' }}>
        {rendered}
      </Text>
    </Tooltip>
  );
}
