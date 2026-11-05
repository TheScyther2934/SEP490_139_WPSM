import { Button, Card, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import { money } from "../../../../utils/format";

export default function PriceFrameTable({ frames, onEdit }) {
  return (
    <Card>
      <TableContainer>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Dịch vụ</TableCell><TableCell>Danh mục</TableCell>
              <TableCell align="right">Giá sàn</TableCell><TableCell align="right">Giá trần</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {frames.length === 0 ? (
              <TableRow><TableCell colSpan={5} align="center">Không có dữ liệu.</TableCell></TableRow>
            ) : frames.map((f) => (
              <TableRow key={f.id} hover>
                <TableCell>
                  <Typography fontWeight={600} color="primary">{f.serviceName}</Typography>
                  <Typography variant="caption" color="text.secondary">Khung giá #{f.id}</Typography>
                </TableCell>
                <TableCell>{f.variant}</TableCell>
                <TableCell align="right">{money(f.minPrice)}</TableCell>
                <TableCell align="right">{money(f.maxPrice)}</TableCell>
                <TableCell align="right"><Button size="small" onClick={() => onEdit(f)}>Đặt khung giá</Button></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}
