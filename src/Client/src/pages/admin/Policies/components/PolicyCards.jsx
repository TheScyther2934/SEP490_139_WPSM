import { Box, Button, Card, CardContent, Stack, Typography } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";

const Row = ({ label, value }) => (
  <Stack direction="row" sx={{ justifyContent: "space-between", py: 0.75 }}>
    <Typography color="text.secondary">{label}</Typography>
    <Typography fontWeight={600}>{value}</Typography>
  </Stack>
);

// Two policy cards: payment ratios (A-07) and CR penalty rule (A-08). T-30 is cumulative (SDS 4.4).
export default function PolicyCards({ policy, onEditDeposit, onEditPenalty }) {
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { md: "1fr 1fr" } }}>
      <Card>
        <CardContent>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography variant="h6">Tỷ lệ các đợt thanh toán</Typography>
            <Button size="small" startIcon={<EditIcon />} onClick={onEditDeposit}>Chỉnh sửa</Button>
          </Stack>
          <Row label="Đợt 1 · NRD khi ký hợp đồng" value={`${policy.nrdRatio}%`} />
          <Row label="Đợt 2 · Mốc T-30 (cộng dồn)" value={`${policy.t30Ratio}%`} />
          <Row label="Quyết toán sau sự kiện" value={`${policy.finalRatio}%`} />
        </CardContent>
      </Card>
      <Card>
        <CardContent>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography variant="h6">Phạt yêu cầu thay đổi sát ngày</Typography>
            <Button size="small" startIcon={<EditIcon />} onClick={onEditPenalty}>Chỉnh sửa</Button>
          </Stack>
          <Row label="Áp dụng khi CR gửi trong vòng" value={`${policy.crPenaltyDays} ngày trước tiệc`} />
          <Row label="Mức phạt trên giá trị thay đổi" value={`${policy.crPenaltyPercent}%`} />
        </CardContent>
      </Card>
    </Box>
  );
}
