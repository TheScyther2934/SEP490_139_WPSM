import { Card, CardContent, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import { money, signedMoney } from "../../utils/format";
import { lineTotal } from "./crMeta";

const deltaColor = (value) => (value > 0 ? "error.main" : value < 0 ? "success.main" : "text.secondary");

// M-08 (and C-07) item comparison: frozen baseline values vs the current proposal (GBR-01/GBR-03).
export default function CrComparisonTable({ items }) {
  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>So sánh thay đổi</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          {items.length} hạng mục đề xuất · baseline gốc không bị thay đổi
        </Typography>
        <TableContainer>
          <Table size="small" id="cr-comparison">
            <TableHead>
              <TableRow>
                <TableCell>Hạng mục dịch vụ</TableCell>
                <TableCell align="right">Baseline đã chốt</TableCell>
                <TableCell align="right">Đề xuất</TableCell>
                <TableCell align="right">Chênh lệch</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((item) => (
                <TableRow key={item.specItemId}>
                  <TableCell>
                    <Typography variant="subtitle2">{item.itemName}</Typography>
                    <Typography variant="caption" color="text.secondary">{item.category}</Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Typography variant="body2">{item.oldQuantity} {item.unit} × {money(item.oldUnitPrice)}</Typography>
                    <Typography variant="caption" color="text.secondary">{money(lineTotal(item.oldQuantity, item.oldUnitPrice))}</Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Typography variant="body2">{item.newQuantity} {item.unit} × {money(item.newUnitPrice)}</Typography>
                    <Typography variant="caption" color="text.secondary">{money(lineTotal(item.newQuantity, item.newUnitPrice))}</Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Typography fontWeight={600} color={deltaColor(item.deltaAmount)}>{signedMoney(item.deltaAmount)}</Typography>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
        <Stack direction="row" spacing={1} sx={{ mt: 2, justifyContent: "flex-end" }}>
          <Typography variant="subtitle2">Tổng chênh lệch hạng mục:</Typography>
          <Typography variant="subtitle2" color={deltaColor(items.reduce((sum, item) => sum + item.deltaAmount, 0))}>
            {signedMoney(items.reduce((sum, item) => sum + item.deltaAmount, 0))}
          </Typography>
        </Stack>
      </CardContent>
    </Card>
  );
}
