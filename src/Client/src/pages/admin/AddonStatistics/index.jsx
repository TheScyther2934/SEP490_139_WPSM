import { Box } from "@mui/material";
import ReportLayout, { ChartWithTable } from "../../../components/reports/ReportLayout";
import BarChart from "../../../components/reports/BarChart";
import { statusLabel } from "../../../components/StatusChip";
import { money, shortMoney } from "../../../utils/format";

const ADDON_COLUMNS = [
  { label: "Trạng thái", value: (r) => statusLabel("addon", r.status) },
  { label: "Số hợp đồng", align: "right", value: (r) => r.count },
  { label: "Giá trị", align: "right", value: (r) => money(r.amount) },
];
const EVENT_COLUMNS = [
  { label: "Trạng thái sự kiện", value: (r) => statusLabel("project", r.status) },
  { label: "Số sự kiện", align: "right", value: (r) => r.count },
];

// Addon contracts created in the period (count and value by status) and events held in the period by status.
export default function AddonStatistics() {
  return (
    <ReportLayout title="Hợp đồng phát sinh và sự kiện" render={(report) => (
      <Box sx={{ display: "grid", gap: 2 }}>
        <ChartWithTable chartTitle="Giá trị phát sinh theo trạng thái"
          chart={<BarChart label="Giá trị phát sinh theo trạng thái" bars={report.addonsByStatus.map((r) => ({ label: statusLabel("addon", r.status), value: r.amount }))} format={shortMoney} />}
          columns={ADDON_COLUMNS} rows={report.addonsByStatus} />
        <ChartWithTable chartTitle="Sự kiện theo trạng thái"
          chart={<BarChart label="Sự kiện theo trạng thái" bars={report.eventsByStatus.map((r) => ({ label: statusLabel("project", r.status), value: r.count }))} format={(v) => String(v)} />}
          columns={EVENT_COLUMNS} rows={report.eventsByStatus} />
      </Box>
    )} />
  );
}
