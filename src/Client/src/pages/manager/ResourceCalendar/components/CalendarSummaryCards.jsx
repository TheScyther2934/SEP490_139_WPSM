import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { percent } from "../../../../utils/format";

// M-21 KPI strip: bookings, hall utilisation, live allocations and conflicts for the visible period.
export default function CalendarSummaryCards({ summary }) {
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr", lg: "repeat(4, 1fr)" }, mb: 2 }}>
      <StatCard label="Tổng tiệc trong kỳ" value={`${summary.events} tiệc cưới`}
        hint={`${summary.bookedSlots}/${summary.availableSlots} ca tiệc đã có dự án`} />
      <StatCard label="Tỷ lệ lấp đầy sảnh" value={percent(summary.hallUtilization)}
        hint="Số ca đã đặt trên tổng số ca còn lại của kỳ" />
      <StatCard label="Phân bổ tài nguyên" value={`${summary.allocations} phân bổ`}
        hint={summary.pendingExceptions ? `${summary.pendingExceptions} đang chờ Admin` : "Không có yêu cầu ngoại lệ"} />
      <StatCard label="Xung đột công suất" value={`${summary.conflicts} xung đột`}
        hint={summary.conflicts ? "Cần đổi thiết bị hoặc xin ngoại lệ" : "Không có xung đột trong kỳ"}
        tone={summary.conflicts ? "error.main" : "text.primary"} />
    </Box>
  );
}
