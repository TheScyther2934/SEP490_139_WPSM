import { Box, Button, Chip, Pagination, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import { money } from "../../../../utils/format";

// A-02 rows with Edit (A-03) and the active toggle; deactivated items stay in old specifications (UC-37).
// The toggle stays the last button of the row (e2e clicks `[data-item] button:last-child`).
export default function CatalogTable({ items, total, page, pages, pageSize, onPage, busy, onEdit, onToggle }) {
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  return (
    <>
      <TableContainer>
        <Table id="catalogTable" size="small" sx={{ "& td, & th": { py: 1.25 } }}>
          <TableHead>
            <TableRow>
              <TableCell sx={{ width: 64 }}>Mã</TableCell><TableCell>Tên dịch vụ</TableCell><TableCell>Danh mục</TableCell><TableCell>Đơn vị</TableCell>
              <TableCell align="right">Giá mặc định</TableCell><TableCell align="center">Trạng thái</TableCell><TableCell align="right">Thao tác</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.length === 0 ? (
              <TableRow><TableCell colSpan={7} align="center" sx={{ py: 4, color: "text.secondary" }}>Không có dịch vụ phù hợp.</TableCell></TableRow>
            ) : items.map((item) => (
              <TableRow key={item.id} hover data-item={item.id}>
                <TableCell sx={{ color: "primary.main", fontWeight: 600 }}>{item.id}</TableCell>
                <TableCell sx={{ fontWeight: 500 }}>{item.name}</TableCell>
                <TableCell>{item.category}</TableCell>
                <TableCell>{item.unit}</TableCell>
                <TableCell align="right" sx={{ fontVariantNumeric: "tabular-nums" }}>{money(item.basePrice)}</TableCell>
                <TableCell align="center">
                  <Chip size="small" label={item.active ? "Đang dùng" : "Ngừng dùng"}
                    sx={item.active ? { bgcolor: "success.light", color: "success.main" } : { bgcolor: "#eceff3", color: "text.secondary" }} />
                </TableCell>
                <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>
                  <Button size="small" onClick={() => onEdit(item)}>Sửa</Button>
                  <Button size="small" color={item.active ? "error" : "success"} disabled={busy} onClick={() => onToggle(item)}>
                    {item.active ? "Ngừng dùng" : "Dùng lại"}
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ p: 2, borderTop: 1, borderColor: "divider", alignItems: "center", justifyContent: "space-between" }}>
        <Typography variant="body2" color="text.secondary">
          {total === 0 ? "0 dịch vụ" : `Hiển thị ${from}–${Math.min(total, page * pageSize)} trên ${total} dịch vụ`}
        </Typography>
        <Box>{pages > 1 && <Pagination page={page} count={pages} shape="rounded" color="primary" onChange={(_, p) => onPage(p)} />}</Box>
      </Stack>
    </>
  );
}
