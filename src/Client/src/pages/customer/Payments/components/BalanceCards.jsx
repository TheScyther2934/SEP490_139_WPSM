import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { money } from "../../../../utils/format";

export default function BalanceCards({ project }) {
  const paid = (project.payments ?? []).filter((p) => p.status === "Paid").reduce((s, p) => s + Number(p.amount), 0);
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { sm: "repeat(3, 1fr)" }, mb: 2 }}>
      <StatCard label="Tổng giá trị hợp đồng" value={money(project.currentTotal || project.baseValue)} />
      <StatCard label="Đã thanh toán" value={money(paid)} />
      <StatCard label="Còn phải thanh toán" value={money(project.outstandingBalance)} tone="secondary" />
    </Box>
  );
}
