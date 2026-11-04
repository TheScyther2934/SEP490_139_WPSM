import BarChart from "../../../../components/reports/BarChart";
import { ChartWithTable } from "../../../../components/reports/ReportLayout";
import { signedMoney } from "../../../../utils/format";

const STATUS = { Approved: "Đã duyệt", Rejected: "Từ chối", Negotiating: "Đang thương lượng", Draft: "Nháp" };
const COLUMNS = [
  { label: "Loại CR", value: (t) => t.type },
  { label: "Số lượng", align: "right", value: (t) => t.count },
  { label: "Giá trị", align: "right", value: (t) => signedMoney(t.amount) },
  { label: "Số ngày xử lý TB", align: "right", value: (t) => (t.averageDays == null ? "—" : t.averageDays.toLocaleString("vi-VN")) },
];

export default function CrStatisticsReport({ report }) {
  return (
    <ChartWithTable
      chartTitle="Số CR theo trạng thái"
      chart={<BarChart label="Số CR theo trạng thái" bars={report.crByStatus.map((s) => ({ label: STATUS[s.status] ?? s.status, value: s.count }))} format={String} />}
      columns={COLUMNS}
      rows={report.crByType}
    />
  );
}
