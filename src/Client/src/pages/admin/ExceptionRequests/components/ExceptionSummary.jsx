import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";

// A-18 KPI strip: the pending queue and how long it has been waiting, plus the decided history.
export default function ExceptionSummary({ summary }) {
  const oldest = summary.oldestPendingHours;
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" }, mb: 2 }}>
      <StatCard label="Chờ duyệt" value={`${summary.pending} yêu cầu`}
        hint={summary.pending === 0 ? "Không có yêu cầu nào đang chờ" : oldest == null ? "Cần quyết định" : `Lâu nhất ${oldest} giờ`}
        tone={summary.pending ? "warning.main" : "text.primary"} />
      <StatCard label="Đã duyệt" value={`${summary.approved} yêu cầu`}
        hint="Đã ghi audit và cập nhật CR / lịch tài nguyên" tone="success.main" />
      <StatCard label="Đã từ chối" value={`${summary.rejected} yêu cầu`}
        hint="Quy tắc chuẩn của trung tâm được giữ nguyên" tone="error.main" />
    </Box>
  );
}
