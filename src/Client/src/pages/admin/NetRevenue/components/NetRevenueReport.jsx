import BarChart from "../../../../components/reports/BarChart";
import { ChartWithTable } from "../../../../components/reports/ReportLayout";
import { money, monthVi, shortMoney } from "../../../../utils/format";

const COLUMNS = [
  { label: "Tháng", value: (r) => monthVi(r.month) },
  { label: "Giá trị hợp đồng", align: "right", value: (r) => money(r.contractValue) },
  { label: "Phát sinh đã xác nhận", align: "right", value: (r) => money(r.addonValue) },
  { label: "Đã thu", align: "right", value: (r) => money(r.received) },
  { label: "Còn phải thu", align: "right", value: (r) => money(r.outstanding) },
];

export default function NetRevenueReport({ report }) {
  return (
    <ChartWithTable
      chartTitle="Doanh thu đã thu theo tháng"
      chart={<BarChart label="Doanh thu đã thu theo tháng" bars={report.revenue.map((r) => ({ label: monthVi(r.month), value: r.received }))} format={shortMoney} />}
      columns={COLUMNS}
      rows={report.revenue}
    />
  );
}
