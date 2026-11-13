import { Box, Button, Card, CardContent, Divider, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import { dateVi } from "../../../../utils/format";

const Row = ({ label, value }) => (
  <Stack direction="row" sx={{ justifyContent: "space-between", py: 0.75 }}>
    <Typography color="text.secondary">{label}</Typography>
    <Typography fontWeight={600}>{value}</Typography>
  </Stack>
);

const EditButton = ({ onClick }) => (
  <Button variant="outlined" startIcon={<EditIcon />} onClick={onClick} sx={{ minWidth: 140, alignSelf: "flex-start" }}>Chỉnh sửa</Button>
);

// Two policy cards (mockup A-06): payment ratios with NRD as the headline (A-07) and the T-day penalty table in force today (A-08).
// T-30 is cumulative (SDS 4.4).
export default function PolicyCards({ policy, rules, onEditDeposit, onEditPenalty }) {
  return (
    <Box sx={{ display: "grid", gap: 3, gridTemplateColumns: { md: "1fr 1fr" }, alignItems: "start" }}>
      <Card>
        <CardContent sx={{ p: 3 }}>
          <Stack spacing={2}>
            <Typography variant="h6">Tỷ lệ các đợt thanh toán</Typography>
            <Box>
              <Typography variant="body2" color="text.secondary">Tỷ lệ đặt cọc không hoàn lại (NRD)</Typography>
              <Typography sx={{ fontSize: 44, fontWeight: 700, lineHeight: 1.2, color: "secondary.main" }}>{policy.nrdRatio} %</Typography>
            </Box>
            <Divider />
            <Box>
              <Row label="Đợt 1 · NRD khi ký hợp đồng" value={`${policy.nrdRatio}%`} />
              <Row label="Đợt 2 · Mốc T-30 (cộng dồn)" value={`${policy.t30Ratio}%`} />
              <Row label="Quyết toán sau sự kiện" value={`${policy.finalRatio}%`} />
            </Box>
            <EditButton onClick={onEditDeposit} />
          </Stack>
        </CardContent>
      </Card>
      <Card>
        <CardContent sx={{ p: 3 }}>
          <Stack spacing={2}>
            <Typography variant="h6">Phạt yêu cầu thay đổi theo T-day</Typography>
            {rules.length === 0 ? <Typography color="text.secondary">Chưa có quy tắc phạt.</Typography> : (
              <Box id="penaltyCard" sx={{ border: 1, borderColor: "divider", borderRadius: 1, overflow: "hidden" }}>
                <Table size="small">
                  <TableHead>
                    <TableRow><TableCell>Khoảng T-day</TableCell><TableCell align="right">Mức phạt</TableCell><TableCell align="right">Hiệu lực từ</TableCell></TableRow>
                  </TableHead>
                  <TableBody>
                    {[...rules].sort((a, b) => b.tDayMax - a.tDayMax).map((r) => (
                      <TableRow key={`${r.tDayMin}-${r.tDayMax}`}>
                        <TableCell>Từ T-{r.tDayMax} đến T-{r.tDayMin}</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 600, color: r.ratePercent > 0 ? "error.main" : "success.main" }}>{r.ratePercent} %</TableCell>
                        <TableCell align="right">{dateVi(r.effectiveFrom)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            )}
            <Typography variant="body2" color="text.secondary">Ngày T-day nằm ngoài mọi khoảng thì không tính phạt.</Typography>
            <EditButton onClick={onEditPenalty} />
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
