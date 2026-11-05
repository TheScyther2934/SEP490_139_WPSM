import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { money, signedMoney } from "../../../../utils/format";

// A gap between the specification total and the contract value is shown only; it never blocks the freeze (UC-20).
export default function SpecSummary({ baseValue, total, count }) {
  const gap = total - baseValue;
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(4, 1fr)" }, mb: 2 }}>
      <StatCard label="Giá trị hợp đồng gốc" value={money(baseValue)} />
      <StatCard label="Tổng đặc tả hiện tại" value={money(total)} />
      <StatCard label="Chênh lệch" value={gap === 0 ? "Khớp" : signedMoney(gap)} tone={gap === 0 ? "primary" : "secondary"} />
      <StatCard label="Số hạng mục" value={count} />
    </Box>
  );
}
