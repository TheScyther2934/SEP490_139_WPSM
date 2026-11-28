import { Alert, Card, CardContent, Stack, Typography } from "@mui/material";
import { dateVi } from "../utils/format";

const Row = ({ label, value, field }) => (
  <Stack direction="row" spacing={2} sx={{ justifyContent: "space-between", py: 0.5 }}>
    <Typography variant="body2" color="text.secondary">{label}</Typography>
    <Typography variant="body2" fontWeight={600} data-field={field}>{value}</Typography>
  </Stack>
);

// The policy an event was signed with (record EventTermsView). A later policy applies to new contracts only.
export default function EventTermsCard({ terms }) {
  if (!terms) return null;
  return (
    <Card id="eventTerms">
      <CardContent>
        <Typography variant="h6">Điều khoản áp dụng</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          Theo chính sách lúc ký hợp đồng ngày {dateVi(terms.signedOn)}
          {terms.policyEffectiveFrom ? ` (chính sách hiệu lực từ ${dateVi(terms.policyEffectiveFrom)})` : ""}.
        </Typography>
        <Row label="Đặt cọc" value={`${terms.depositRatio}%`} field="terms-deposit" />
        <Row label="Giảm tối đa so với lúc ký" value={`${terms.maxDecreaseRatio}%`} field="terms-decrease" />
        <Row label="Khóa dịch vụ trước ngày cưới" value={`${terms.lockDays} ngày`} field="terms-lock" />
        <Row label="Hạn đặt nhà cung cấp trước ngày cưới" value={`${terms.supplierDeadlineDays} ngày`} field="terms-supplier" />
        <Row label="Hạn tất toán sau ngày cưới" value={`${terms.settlementDueDays} ngày`} field="terms-settlement" />
        {terms.differsFromCurrent && (
          <Alert severity="info" sx={{ mt: 1 }} data-field="terms-older">
            Chính sách hiện tại đã thay đổi, nhưng chỉ áp dụng cho hợp đồng ký sau. Sự kiện này giữ điều khoản trên.
          </Alert>
        )}
      </CardContent>
    </Card>
  );
}
