import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { signedMoney } from "../../../../utils/format";

// C-05 header tiles: volume, negotiation load and the approved money impact.
export default function CrSummaryCards({ summary }) {
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr", lg: "repeat(4, 1fr)" }, mb: 2 }}>
      <StatCard label="Tổng yêu cầu" value={`${summary.total} CR`} hint="Đã ghi nhận trong hồ sơ dự án" />
      <StatCard label="Đang thương lượng" value={`${summary.negotiating} yêu cầu`}
        hint={summary.draft ? `${summary.draft} bản nháp chưa gửi` : "Không có bản nháp"} tone="warning.main" />
      <StatCard label="Đã duyệt" value={`${summary.approved} yêu cầu`} hint={summary.rejected ? `${summary.rejected} yêu cầu bị từ chối` : "Không có yêu cầu bị từ chối"} tone="success.main" />
      <StatCard label="Tác động ngân sách ròng" value={signedMoney(summary.approvedImpact)}
        hint="Tổng chênh lệch của các yêu cầu đã duyệt" tone={summary.approvedImpact > 0 ? "error.main" : "text.primary"} />
    </Box>
  );
}
