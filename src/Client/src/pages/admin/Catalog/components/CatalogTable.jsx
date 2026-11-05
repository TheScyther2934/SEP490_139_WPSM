import { Card, Chip, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import { money } from "../../../../utils/format";

export default function CatalogTable({ items }) {
  return (
    <Card>
      <TableContainer>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Tên dịch vụ</TableCell><TableCell>Danh mục</TableCell><TableCell>Đơn vị</TableCell>
              <TableCell align="right">Giá mặc định</TableCell><TableCell align="center">Trạng thái</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.length === 0 ? (
              <TableRow><TableCell colSpan={5} align="center">Không có dữ liệu.</TableCell></TableRow>
            ) : items.map((item) => (
              <TableRow key={item.id} hover>
                <TableCell>
                  <Typography fontWeight={600} color="primary">{item.name}</Typography>
                  <Typography variant="caption" color="text.secondary">Mã: {item.id}</Typography>
                </TableCell>
                <TableCell>{item.category}</TableCell>
                <TableCell>{item.unit}</TableCell>
                <TableCell align="right">{money(item.basePrice)}</TableCell>
                <TableCell align="center">
                  <Chip size="small" label={item.active ? "Đang dùng" : "Ngừng dùng"} color={item.active ? "success" : "default"} variant={item.active ? "filled" : "outlined"} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}
