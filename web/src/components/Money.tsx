import { Text } from '@mantine/core';
import { formatMoney, formatPnl, formatPrice, formatQuantity, formatR, signOf } from '@/lib/format';
import { PNL_COLORS } from '@/lib/theme';

type Numeric = number | null | undefined;

interface MoneyProps {
  value: Numeric;
  currency?: string;
  size?: string;
  fw?: number;
}

export function Money({ value, currency, size = 'sm', fw }: MoneyProps) {
  return (
    <Text component="span" size={size} fw={fw} ff="monospace">
      {formatMoney(value, { currency })}
    </Text>
  );
}

export function Price({ value, size = 'sm' }: { value: Numeric; size?: string }) {
  return (
    <Text component="span" size={size} ff="monospace">
      {formatPrice(value)}
    </Text>
  );
}

export function Quantity({ value, size = 'sm' }: { value: Numeric; size?: string }) {
  return (
    <Text component="span" size={size} ff="monospace">
      {formatQuantity(value)}
    </Text>
  );
}

/** An R multiple, coloured and signed like PnL. */
export function RMultiple({ value, size = 'sm' }: { value: Numeric; size?: string }) {
  return (
    <Text component="span" size={size} ff="monospace" c={PNL_COLORS[signOf(value)]}>
      {formatR(value)}
    </Text>
  );
}

/**
 * Signed money. The sign is explicit as well as the colour, so the number is
 * still readable to anyone who cannot tell the two apart.
 */
export function Pnl({ value, currency, size = 'sm', fw = 500 }: MoneyProps) {
  return (
    <Text component="span" size={size} fw={fw} ff="monospace" c={PNL_COLORS[signOf(value)]}>
      {formatPnl(value, { currency })}
    </Text>
  );
}
