import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { signedAmount } from "../../../../components/changeRequest/crMeta";

// M-07 pipeline tiles: how many tickets exist, how many are still being negotiated, and the money at stake.
export default function CrSummaryCards({ summary }) {
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr", lg: "repeat(4, 1fr)" }, mb: 2 }}>
      <StatCard label="Tổng CR phát sinh" value={`${summary.total} yêu cầu`}
        hint={summary.pendingImpact ? `Giá trị đang đàm phán ${signedAmount(summary.pendingImpact)}` : "Chưa có giá trị đang đàm phán"} />
      <StatCard label="Đang thương lượng / Duyệt" value={`${summary.negotiating} yêu cầu`}
        hint={summary.draft ? `${summary.draft} bản nháp chưa gửi khách` : "Không có bản nháp"} tone="warning.main" />
      <StatCard label="Đã duyệt" value={`${summary.approved} yêu cầu`}
        hint={summary.approvedImpact ? `Đã cộng vào hợp đồng ${signedAmount(summary.approvedImpact)}` : "Chưa có phụ lục nào được tạo"} tone="success.main" />
      <StatCard label="Bị từ chối" value={`${summary.rejected} yêu cầu`} hint="Hồ sơ vẫn được lưu để đối soát" tone="error.main" />
    </Box>
  );
}
