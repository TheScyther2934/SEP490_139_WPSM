import { Box } from "@mui/material";
import StatCard from "./StatCard";
import { money, signedMoney } from "../utils/format";

// GBR-08 figures from GET /projects/{id}/payments, shared by C-10 and M-13.
export default function PaymentSummary({ payments }) {
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { sm: "repeat(2, 1fr)", md: "repeat(5, 1fr)" }, mb: 2 }} id="paymentSummary">
      <StatCard label="Giá trị hợp đồng gốc" value={money(payments.baseValue)} />
      <StatCard label="Phát sinh từ CR đã duyệt" value={signedMoney(payments.approvedCrVariance)} />
      <StatCard label="Tổng hiện tại" value={money(payments.currentTotal)} />
      <StatCard label="Đã thanh toán" value={money(payments.totalPaid)} />
      <StatCard label="Còn lại" value={money(payments.outstanding)} tone={payments.outstanding > 0 ? "secondary" : "primary"} />
    </Box>
  );
}
