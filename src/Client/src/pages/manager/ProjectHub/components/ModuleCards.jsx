import { Box, LinearProgress } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { PATHS } from "../../../../routes/paths";
import { money } from "../../../../utils/format";

// One tile per project module; each opens its tab.
export default function ModuleCards({ project, roadmap }) {
  const M = PATHS.manager;
  const code = project.id;
  const done = roadmap.filter((t) => t.status === "Done").length;
  const next = roadmap.find((t) => t.status !== "Done");
  const nrd = project.payments?.find((p) => p.type === "NRD");
  const latestCrCode = project.changes?.[0]?.title || "latest";
  const beoHint = project.beoStatus === "Stale" ? "Cần kiểm tra và duyệt lại" : project.beoStatus === "Not started" ? "Dự án chưa có BEO" : `Trạng thái: ${project.beoStatus}`;
  const roadmapHint = !roadmap.length ? "Tạo sau khi chốt baseline" : next ? `${done}/${roadmap.length} · Tiếp theo: ${next.title}` : "Đã hoàn thành lộ trình";

  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { sm: "1fr 1fr", xl: "repeat(4, 1fr)" } }}>
      <StatCard label="1. Hợp đồng gốc" value={project.status === "Active Baseline" ? "Đang soạn đặc tả" : "Baseline đã chốt"}
        hint={`${project.specificationSummary} · ${money(project.baseValue)}`} to={M.specification(code)} />
      <StatCard label="2. Tiến độ lộ trình" value={`${project.roadmapProgress}%`} hint={roadmapHint} to={M.roadmap(code)}>
        <LinearProgress variant="determinate" value={project.roadmapProgress} aria-label="Tiến độ lộ trình" sx={{ mt: 1 }} />
      </StatCard>
      <StatCard label="3. Yêu cầu thay đổi" value={`${project.openChangeRequests} đang xử lý`}
        hint={project.openChangeRequests ? "Có yêu cầu cần xử lý" : "Không có CR đang mở"} to={M.changeRequests(code)} />
      <StatCard label="4. Tác động chi phí & Phụ lục" value={`${project.addendumCount || 0} phụ lục`}
        hint="Phê duyệt chi phí CR & phát hành phụ lục (M-09)" to={M.costImpact(code, latestCrCode)} />
      <StatCard label="5. BEO & Điều phối" value={project.beoStatus} hint={beoHint} to={M.beo(code)} tone={project.beoStatus === "Stale" ? "error" : "primary"} />
      <StatCard label="6. Dư nợ thanh toán" value={money(project.outstandingBalance)}
        hint={`NRD: ${nrd?.status === "Paid" ? "Đã thu" : "Chưa xác nhận thu"} · Tổng HĐ: ${money(project.currentTotal)}`} to={M.payments(code)} />
      <StatCard label="7. Ngày sự kiện" value={project.dayOfTotal ? `${project.dayOfCompleted}/${project.dayOfTotal} hoàn thành` : "Chưa có nhiệm vụ"} to={M.dayOf(code)} />
      <StatCard label="8. Khiếu nại đang mở" value={project.openComplaints || 0} hint={project.openComplaints ? "Cần phản hồi" : "Chưa có khiếu nại"} to={M.complaints(code)} />
    </Box>
  );
}
