import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { PATHS } from "../../../../routes/paths";
import { percent, shortMoney } from "../../../../utils/format";

// A-09 headline numbers; each tile opens its detailed report with the same range.
export default function ReportSummary({ report, query }) {
  const A = PATHS.admin;
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { sm: "repeat(3, 1fr)" } }}>
      <StatCard label="Công suất sảnh" value={percent(report.utilization)} to={`${A.hallUtilization}${query}`} />
      <StatCard label="Doanh thu thuần (đã thu)" value={shortMoney(report.netRevenue)} to={`${A.netRevenue}${query}`} />
      <StatCard label="CR đã duyệt" value={report.crsApproved} to={`${A.crStatistics}${query}`} />
    </Box>
  );
}
