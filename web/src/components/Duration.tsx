import { Text, Tooltip } from '@mantine/core';
import { formatDuration } from '@/lib/time';

/**
 * A .NET `TimeSpan`. `new Date()` cannot parse `"1.03:20:00"` at all, so this
 * goes through `parseTimeSpan` rather than the Date constructor.
 */
export function Duration({
  value,
  size = 'sm',
}: {
  value: string | null | undefined;
  size?: string;
}) {
  const rendered = formatDuration(value);

  if (!value) {
    return (
      <Text component="span" size={size} c="dimmed">
        {rendered}
      </Text>
    );
  }

  return (
    <Tooltip label={value} withArrow openDelay={400}>
      <Text component="span" size={size} style={{ whiteSpace: 'nowrap' }}>
        {rendered}
      </Text>
    </Tooltip>
  );
}
