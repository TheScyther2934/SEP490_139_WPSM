import { Box } from "@mui/material";
import StatCard from "../../../../components/StatCard";
import { money, signedMoney } from "../../../../utils/format";

// Signed value, the total being edited, the change against the saved total and the lowest total allowed in stage 2.
export default function SpecSummary({ signedValue, savedTotal, total, minimumTotal }) {
  const change = total - savedTotal;
  const belowFloor = total < minimumTotal;
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(4, 1fr)" }, mb: 2 }}>
      <StatCard label="Giá trị lúc ký" value={money(signedValue)} />
      <StatCard label="Tổng sau khi sửa" value={money(total)} tone={belowFloor ? "error" : "primary"} hint={belowFloor ? "Thấp hơn mức tối thiểu" : undefined} />
      <StatCard label="Thay đổi so với hiện tại" value={change === 0 ? "Chưa thay đổi" : signedMoney(change)} tone={change < 0 ? "secondary" : "primary"} />
      <StatCard label="Mức tối thiểu được phép" value={money(minimumTotal)} hint="Không giảm quá 30% giá trị lúc ký" />
    </Box>
  );
}
