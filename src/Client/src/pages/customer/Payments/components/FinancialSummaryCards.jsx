import { Box, Card, CardContent, Typography } from "@mui/material";
import { money, signedMoney } from "../../../../utils/format";

export default function FinancialSummaryCards({ summary }) {
  if (!summary) return null;

  const cards = [
    {
      label: "Giá trị hợp đồng gốc",
      value: money(summary.baseContractValue),
      hint: "Giá trị theo Contract Baseline",
      tone: "text.primary",
    },
    {
      label: "Chênh lệch CR đã duyệt",
      value: signedMoney(summary.totalApprovedCrVariance),
      hint: summary.totalApprovedCrVariance !== 0 ? "Tổng chi phí phát sinh CR" : "Chưa có phát sinh",
      tone: summary.totalApprovedCrVariance > 0 ? "warning.main" : summary.totalApprovedCrVariance < 0 ? "info.main" : "text.secondary",
    },
    {
      label: "Tổng giá trị hiện tại",
      value: money(summary.currentProjectTotal),
      hint: "Giá trị gốc + CR được duyệt",
      tone: "primary.main",
    },
    {
      label: "Đã thanh toán",
      value: money(summary.totalAmountPaid),
      hint: "Tổng các đợt đã xác nhận thu",
      tone: "success.main",
    },
    {
      label: "Còn phải thanh toán",
      value: money(summary.outstandingBalance),
      hint: summary.outstandingBalance === 0 ? "Đã thanh toán đủ toàn bộ" : "Dư nợ còn lại cần thanh toán",
      tone: summary.outstandingBalance > 0 ? "error.main" : "success.main",
      highlight: true,
    },
  ];

  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(3, 1fr)", lg: "repeat(5, 1fr)" },
        gap: 2,
        mb: 3,
      }}
    >
      {cards.map((c, idx) => (
        <Card
          key={idx}
          sx={{
            height: "100%",
            border: c.highlight ? "1px solid" : undefined,
            borderColor: c.highlight ? (summary.outstandingBalance > 0 ? "error.light" : "success.light") : undefined,
            bgcolor: c.highlight && summary.outstandingBalance > 0 ? "action.hover" : undefined,
          }}
        >
          <CardContent>
            <Typography variant="body2" color="text.secondary" gutterBottom>
              {c.label}
            </Typography>
            <Typography variant="h5" sx={{ color: c.tone, fontWeight: 700, mt: 0.5 }}>
              {c.value}
            </Typography>
            <Typography variant="caption" color="text.secondary" component="p" sx={{ mt: 0.5 }}>
              {c.hint}
            </Typography>
          </CardContent>
        </Card>
      ))}
    </Box>
  );
}
