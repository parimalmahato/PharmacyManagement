
import {
  ResponsiveContainer,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
} from "recharts";

import type { DailySales } from "../api/reports";

interface Props {
  data: DailySales[];
}

const currencyFormatter = new Intl.NumberFormat("en-IN", {
  style: "currency",
  currency: "INR",
  maximumFractionDigits: 0,
});

export default function DailyRevenueChart({ data }: Props) {
  if (data.length === 0) {
    return <p>No sales data available for the selected dates.</p>;
  }

  const chartData = data.map((item) => ({
    date: item.date,
    revenue: item.revenue,
    netSales: item.netSales,
    netCollections: item.netCollections,
    salesCount: item.salesCount,
  }));

  return (
    <div style={{ width: "100%", height: 320 }}>
      <ResponsiveContainer width="100%" height="100%">
        <BarChart
          data={chartData}
          margin={{ top: 10, right: 20, left: 10, bottom: 20 }}
        >
          <CartesianGrid strokeDasharray="3 3" />

          <XAxis
            dataKey="date"
            tick={{ fontSize: 11 }}
            minTickGap={20}
          />

          <YAxis
            tickFormatter={(value: number) =>
              currencyFormatter.format(value)
            }
            width={85}
          />

          <Tooltip
            formatter={(value) =>
              currencyFormatter.format(Number(value ?? 0))
            }
          />

          <Bar
            dataKey="revenue"
            name="Invoiced sales"
            fill="#2563eb"
            radius={[5, 5, 0, 0]}
          />
          <Bar dataKey="netSales" name="Net sales" fill="#059669" />
          <Bar dataKey="netCollections" name="Net collections" fill="#7c3aed" />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
