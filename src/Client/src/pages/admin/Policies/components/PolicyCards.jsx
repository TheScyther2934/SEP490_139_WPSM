import { Alert, Box, Button, Card, CardContent, Divider, Stack, Typography } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";

const Row = ({ label, value }) => (
  <Stack direction="row" spacing={2} sx={{ justifyContent: "space-between", py: 0.75 }}>
    <Typography color="text.secondary">{label}</Typography>
    <Typography fontWeight={600} sx={{ textAlign: "right" }}>{value}</Typography>
  </Stack>
);

// The rules of the five stages, as Admin configured them.
export default function PolicyCards({ policy: p, onEdit }) {
  return (
    <Box sx={{ display: "grid", gap: 3, gridTemplateColumns: { md: "1fr 1fr" }, alignItems: "start" }}>
      <Alert id="policyTermsNote" severity="info" sx={{ gridColumn: { md: "1 / -1" } }}>
        Đang áp dụng cho hợp đồng ký từ {p.effectiveFrom ? new Date(p.effectiveFrom).toLocaleDateString("vi-VN") : "đầu"}.{" "}
        {p.eventsOnOlderTerms > 0
          ? <>Có <b data-field="events-on-older-terms">{p.eventsOnOlderTerms}</b> sự kiện đang diễn ra theo điều khoản cũ (giữ nguyên điều khoản lúc ký).</>
          : "Mọi sự kiện đang diễn ra đều theo chính sách này."}
      </Alert>
      <Card>
        <CardContent sx={{ p: 3 }}>
          <Typography variant="h6">Thanh toán</Typography>
          <Typography sx={{ fontSize: 44, fontWeight: 700, lineHeight: 1.2, color: "secondary.main" }}>{p.depositRatio} %</Typography>
          <Typography variant="body2" color="text.secondary">Tiền đặt cọc khi ký (không hoàn khi khách hủy)</Typography>
          <Divider sx={{ my: 2 }} />
          <Row label="Lần 1 · Đặt cọc khi ký hợp đồng" value={`${p.depositRatio}%`} />
          <Row label="Lần 2 · Tất toán sau ngày cưới" value="Phần còn lại + phát sinh" />
          <Row label="Hạn tất toán" value={`${p.settlementDueDays} ngày sau ngày cưới`} />
        </CardContent>
      </Card>
      <Card>
        <CardContent sx={{ p: 3 }}>
          <Typography variant="h6" sx={{ mb: 1 }}>Các giai đoạn</Typography>
          <Row label="Giai đoạn 1 · Ký hợp đồng khi ngày cưới cách" value={`${p.bookingMinDays} – ${p.bookingMaxDays} ngày`} />
          <Row label="Giai đoạn 2 · Giảm tối đa so với lúc ký" value={`${p.maxDecreaseRatio}%`} />
          <Row label="Giai đoạn 3 · Tự khóa dịch vụ trước ngày cưới" value={`${p.lockDays} ngày`} />
          <Row label="Hạn đặt nhà cung cấp trước ngày cưới" value={`${p.supplierDeadlineDays} ngày`} />
          <Divider sx={{ my: 2 }} />
          <Row label="Hotline hiển thị cho khách" value={p.hotline} />
        </CardContent>
      </Card>
      <Button variant="outlined" startIcon={<EditIcon />} onClick={onEdit} sx={{ justifySelf: "start" }}>Chỉnh sửa chính sách</Button>
    </Box>
  );
}
