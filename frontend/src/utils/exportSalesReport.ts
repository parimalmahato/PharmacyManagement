
import type { SalesReport } from "../api/reports";

function escapeCsv(value: string | number): string {
  const text = String(value);

  // Prevent spreadsheet formula execution.
  const safeText = /^[\s]*[=+\-@\t\r]/.test(text)
    ? `'${text}`
    : text;

  return `"${safeText.replace(/"/g, '""')}"`;
}

export function exportSalesReport(report: SalesReport): void {
  const rows: Array<Array<string | number>> = [
    ["Pharmacy Sales Report"],
    ["Start Date", report.startDate],
    ["End Date", report.endDate],
    ["Total Sales", report.totalSales],
    ["Time zone", "Asia/Kolkata"],
    ["Invoiced Sales", report.totalRevenue],
    ["Return Credits", report.returnCredits], ["Net Sales", report.netSales],
    ["Collections", report.collections], ["Refunds", report.refunds], ["Net Collections", report.netCollections],
    ["Current Outstanding - Invoices In Range", report.outstandingBalance], ["Current Refund Due - Invoices In Range", report.refundDue],
    ["Received Purchase Invoices", report.receivedPurchases], ["Purchase Value - Invoice Date", report.purchaseValue],
    [],
    ["Daily Sales"],
    ["Date", "Sales Count", "Invoiced Sales", "Return Credits", "Net Sales", "Collections", "Refunds", "Net Collections"],
    ...report.dailySales.map((item) => [
      item.date,
      item.salesCount,
      item.revenue,
      item.returnCredits, item.netSales, item.collections, item.refunds, item.netCollections,
    ]),
    [],
    ["Collections By Method"],
    ["Method", "Collections", "Refunds", "Net Collections"],
    ...report.collectionsByMethod.map(row => [row.method, row.collections, row.refunds, row.netCollections]),
    [],
    ["Top Selling Medicines - Original Invoiced Volume"],
    ["Medicine", "Quantity Sold", "Revenue"],
    ...report.topSellingMedicines.map((item) => [
      item.medicineName,
      item.quantitySold,
      item.revenue,
    ]),
  ];

  const csvContent = rows
    .map((row) => row.map(escapeCsv).join(","))
    .join("\r\n");

  const blob = new Blob(
    ["\uFEFF", csvContent],
    { type: "text/csv;charset=utf-8;" }
  );

  const url = URL.createObjectURL(blob);

  const link = document.createElement("a");
  link.href = url;
  link.download =
    `sales-report-${report.startDate}-to-${report.endDate}.csv`;

  document.body.appendChild(link);
  link.click();
  link.remove();

  URL.revokeObjectURL(url);
}
