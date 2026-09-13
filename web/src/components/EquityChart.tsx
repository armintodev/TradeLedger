import { AreaChart } from '@mantine/charts';
import { formatMoney } from '@/lib/format';

export interface EquityChartProps {
  data: { at: string; equity: number }[];
}

/**
 * Lazily imported so Recharts stays out of the initial bundle — the dashboard
 * is the index route and this is by far the heaviest thing on it.
 */
export default function EquityChart({ data }: EquityChartProps) {
  return (
    <AreaChart
      h={260}
      data={data}
      dataKey="at"
      series={[{ name: 'equity', label: 'Equity', color: 'indigo.5' }]}
      curveType="linear"
      withDots={data.length < 40}
      valueFormatter={(value) => formatMoney(value)}
      yAxisProps={{ width: 80, domain: ['auto', 'auto'] }}
      gridAxis="xy"
    />
  );
}
