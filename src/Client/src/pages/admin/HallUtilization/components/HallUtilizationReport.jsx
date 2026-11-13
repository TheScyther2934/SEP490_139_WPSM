import BarChart from "../../../../components/reports/BarChart";
import { ChartWithTable } from "../../../../components/reports/ReportLayout";
import { percent } from "../../../../utils/format";

const COLUMNS = [
  { label: "Sảnh", value: (h) => h.hall },
  { label: "Ca trống", align: "right", value: (h) => h.available },
  { label: "Ca đã đặt", align: "right", value: (h) => h.booked },
  { label: "Công suất", align: "right", value: (h) => percent(h.utilization) },
];

export default function HallUtilizationReport({ report }) {
  return (
    <ChartWithTable
      chartTitle="Công suất theo sảnh"
      chart={<BarChart label="Công suất theo sảnh" bars={report.halls.map((h) => ({ label: h.hall, value: h.utilization }))} format={percent} max={100} />}
      columns={COLUMNS}
      rows={report.halls}
    />
  );
}
