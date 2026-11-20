import { Box, Card, CardContent, Typography } from "@mui/material";
import { money } from "../../../../utils/format";

export default function FinancialSummaryCards({ summary }) {
  if (!summary) return null;

  const cards = [
    {
      label: "Hợp đồng sau điều chỉnh",
      value: money(summary.baseContractValue),
      hint: "Tổng các dịch vụ đã đặt",
      tone: "text.primary",
    },
    {
      label: "Phát sinh đã xác nhận",
      value: money(summary.confirmedAddonTotal),
      hint: summary.confirmedAddonTotal ? "Cộng vào hóa đơn tất toán" : "Chưa có phát sinh",
      tone: summary.confirmedAddonTotal > 0 ? "warning.main" : "text.secondary",
    },
    {
      label: "Tổng giá trị hiện tại",
      value: money(summary.currentProjectTotal),
      hint: "Hợp đồng + phát sinh",
      tone: "primary.main",
    },
    {
      label: "Đã thanh toán",
      value: money(summary.totalAmountPaid),
      hint: "Đặt cọc và tất toán đã thu",
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
