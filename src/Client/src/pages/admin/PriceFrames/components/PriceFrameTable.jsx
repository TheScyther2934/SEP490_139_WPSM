import { Button, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from "@mui/material";
import { money } from "../../../../utils/format";

// Rendered inside the page card, under its search toolbar.
export default function PriceFrameTable({ frames, onEdit, onHistory }) {
  return (
    <TableContainer>
      <Table size="small" sx={{ "& td, & th": { py: 1.25 } }}>
        <TableHead>
          <TableRow>
            <TableCell sx={{ width: 64 }}>Mã</TableCell><TableCell>Dịch vụ</TableCell><TableCell>Danh mục</TableCell>
            <TableCell align="right">Giá sàn</TableCell><TableCell align="right">Giá trần</TableCell><TableCell align="right">Thao tác</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {frames.length === 0 ? (
            <TableRow><TableCell colSpan={6} align="center" sx={{ py: 4, color: "text.secondary" }}>Không có khung giá phù hợp.</TableCell></TableRow>
          ) : frames.map((f) => (
            <TableRow key={f.id} hover>
              <TableCell sx={{ color: "primary.main", fontWeight: 600 }}>{f.id}</TableCell>
              <TableCell sx={{ fontWeight: 500 }}>{f.serviceName}</TableCell>
              <TableCell>{f.variant}</TableCell>
              <TableCell align="right" sx={{ fontVariantNumeric: "tabular-nums" }}>{money(f.minPrice)}</TableCell>
              <TableCell align="right" sx={{ fontVariantNumeric: "tabular-nums" }}>{money(f.maxPrice)}</TableCell>
              <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>
                <Button size="small" onClick={() => onHistory(f)}>Lịch sử</Button>
                <Button size="small" onClick={() => onEdit(f)}>Đặt khung giá</Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
