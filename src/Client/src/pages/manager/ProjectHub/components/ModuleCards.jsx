import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { statusLabel } from "../../../../components/StatusChip";
import { PATHS } from "../../../../routes/paths";
import { dateVi, money } from "../../../../utils/format";

const coordinationHint = (p) => {
  if (!p.dayOfTotal) return p.status === "Adjusting" ? "Tạo sau khi khóa dịch vụ" : "Tạo từ các dịch vụ đã khóa";
  const parts = [p.dayOfOverdue && `${p.dayOfOverdue} quá hạn`, p.dayOfMissingProof && `${p.dayOfMissingProof} thiếu minh chứng`].filter(Boolean);
  return parts.length ? parts.join(" · ") : "Theo 4 bộ phận, có ảnh minh chứng";
};

// One tile per area of the event; each opens its tab.
export default function ModuleCards({ project }) {
  const M = PATHS.manager;
  const code = project.id;
  const deposit = project.payments?.find((p) => p.type === "Deposit");
  const beoHint = project.beoStatus === "Stale" ? "Cần kiểm tra và duyệt lại"
    : project.beoStatus === "Not started" ? "Tạo khi dịch vụ đã khóa" : statusLabel("beo", project.beoStatus);
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { sm: "1fr 1fr", xl: "repeat(4, 1fr)" } }}>
      <StatCard label="Dịch vụ đã đặt" value={money(project.currentTotal)}
        hint={project.canAdjust ? `Còn điều chỉnh đến ${dateVi(project.lockDate)}` : `${project.specificationSummary} · đã khóa`} to={M.specification(code)} />
      <StatCard label="Nhà cung cấp" value={project.status === "Adjusting" ? "Chưa đến bước" : "Đặt và tải minh chứng"}
        hint={`Hạn: ${dateVi(project.supplierDeadline)}`} to={M.suppliers(code)} />
      <StatCard label="Hợp đồng phát sinh" value={`${project.addonCount || 0} hợp đồng`}
        hint={project.pendingAddons ? `${project.pendingAddons} chờ khách xác nhận` : project.canAddAddon ? "Tạo khi khách cần thêm dịch vụ" : "Chỉ có sau khi khóa"}
        to={M.addons(code)} tone={project.pendingAddons ? "warning" : "primary"} />
      <StatCard label="Phiếu điều phối" value={statusLabel("beo", project.beoStatus === "Not started" ? "None" : project.beoStatus)} hint={beoHint}
        to={M.beo(code)} tone={project.beoStatus === "Stale" ? "error" : "primary"} />
      <StatCard label="Còn phải thu" value={money(project.outstandingBalance)}
        hint={`Đặt cọc: ${deposit?.status === "Paid" ? "đã thu" : "chưa xác nhận"} · Phát sinh đã xác nhận: ${money(project.confirmedAddonTotal)}`} to={M.payments(code)} />
      <StatCard label="Điều phối" value={project.dayOfTotal ? `${project.dayOfCompleted}/${project.dayOfTotal} nhiệm vụ xong` : "Chưa có nhiệm vụ"}
        hint={coordinationHint(project)} to={M.departmentSheets(code)}
        tone={project.dayOfOverdue ? "error" : project.dayOfMissingProof ? "warning" : "primary"} />
      <StatCard label="Khiếu nại đang mở" value={project.openComplaints || 0} hint={project.openComplaints ? "Cần phản hồi" : "Chưa có khiếu nại"} to={M.complaints(code)} />
    </Box>
  );
}
