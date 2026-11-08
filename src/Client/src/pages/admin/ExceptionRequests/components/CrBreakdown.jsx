import { Table, TableBody, TableCell, TableHead, TableRow, Typography } from "@mui/material";
import { money, signedMoney } from "../../../../utils/format";

// UC-47 step 2 for a CR waiver: the CR cost breakdown the Admin must review before deciding.
export default function CrBreakdown({ cr }) {
  const withoutWaiver = cr.totalImpact;
  const withWaiver = cr.priceDelta;
  return (
    <>
      <Typography variant="subtitle2" gutterBottom>Chi tiết tài chính của {cr.crCode} (M-08)</Typography>
      <Table size="small" id="a18CrBreakdown">
        <TableHead>
          <TableRow>
            <TableCell>Hạng mục</TableCell>
            <TableCell align="right">Khung / Tỷ lệ</TableCell>
            <TableCell align="right">Số tiền</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          <TableRow>
            <TableCell>Chênh lệch giá trị hạng mục của CR</TableCell>
            <TableCell align="right">—</TableCell>
            <TableCell align="right">{signedMoney(cr.priceDelta)}</TableCell>
          </TableRow>
          <TableRow>
            <TableCell>Phí phạt phát sinh thay đổi muộn (T-day)</TableCell>
            <TableCell align="right">{(Number(cr.penaltyRate) || 0).toLocaleString("vi-VN")} %</TableCell>
            <TableCell align="right" sx={{ color: "error.main" }}>{money(cr.penaltyFee)}</TableCell>
          </TableRow>
          <TableRow>
            <TableCell><strong>Tổng tác động CR nếu không miễn</strong></TableCell>
            <TableCell align="right">—</TableCell>
            <TableCell align="right"><strong>{signedMoney(withoutWaiver)}</strong></TableCell>
          </TableRow>
          <TableRow>
            <TableCell><strong>Nếu được duyệt miễn phạt</strong></TableCell>
            <TableCell align="right">Tiết kiệm</TableCell>
            <TableCell align="right">
              <strong>{money(withWaiver)}</strong>
              <Typography variant="caption" color="success.main" component="p">{money(cr.penaltyFee)}</Typography>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </>
  );
}
