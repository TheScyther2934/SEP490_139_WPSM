import { Box, Button, Chip, Pagination, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import { dateVi, roleLabel } from "../../../../utils/format";

// A-19 rows. The admin's own row cannot be locked (the API refuses it too). The lock button stays last in the row
// so e2e can click `[data-account] button:last-child`.
export default function AccountTable({ items, total, page, pages, pageSize, onPage, busy, currentUserId, onEdit, onReset, onToggle }) {
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  return (
    <>
      <TableContainer>
        <Table id="accountTable" size="small" sx={{ "& td, & th": { py: 1.25 } }}>
          <TableHead>
            <TableRow>
              <TableCell sx={{ width: 64 }}>Mã</TableCell><TableCell>Họ tên</TableCell><TableCell>Email</TableCell><TableCell>Điện thoại</TableCell>
              <TableCell>Vai trò</TableCell><TableCell align="center">Dự án đang mở</TableCell><TableCell>Ngày tạo</TableCell>
              <TableCell align="center">Trạng thái</TableCell><TableCell align="right">Thao tác</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.length === 0 ? (
              <TableRow><TableCell colSpan={9} align="center" sx={{ py: 4, color: "text.secondary" }}>Không có tài khoản phù hợp.</TableCell></TableRow>
            ) : items.map((a) => {
              const self = a.id === currentUserId;
              return (
                <TableRow key={a.id} hover data-account={a.email}>
                  <TableCell sx={{ color: "primary.main", fontWeight: 600 }}>{a.id}</TableCell>
                  <TableCell sx={{ fontWeight: 500 }}>
                    {a.fullName}{self && <Typography component="span" variant="caption" color="text.secondary"> (bạn)</Typography>}
                  </TableCell>
                  <TableCell>{a.email}</TableCell>
                  <TableCell>{a.phone || "—"}</TableCell>
                  <TableCell>{roleLabel(a.role)}</TableCell>
                  <TableCell align="center">{a.openProjects}</TableCell>
                  <TableCell>{dateVi(a.createdAt)}</TableCell>
                  <TableCell align="center">
                    <Chip size="small" data-status label={a.active ? (a.mustChangePassword ? "Chờ đổi mật khẩu" : "Hoạt động") : "Đã khóa"}
                      sx={a.active ? { bgcolor: "success.light", color: "success.main" } : { bgcolor: "#eceff3", color: "text.secondary" }} />
                  </TableCell>
                  <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>
                    <Button size="small" onClick={() => onEdit(a)}>Sửa</Button>
                    <Button size="small" disabled={busy || !a.active} onClick={() => onReset(a)}>Cấp lại mật khẩu</Button>
                    <Button size="small" color={a.active ? "error" : "success"} disabled={busy || self} onClick={() => onToggle(a)}>
                      {a.active ? "Khóa" : "Mở khóa"}
                    </Button>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ p: 2, borderTop: 1, borderColor: "divider", alignItems: "center", justifyContent: "space-between" }}>
        <Typography variant="body2" color="text.secondary">
          {total === 0 ? "0 tài khoản" : `Hiển thị ${from}–${Math.min(total, page * pageSize)} trên ${total} tài khoản`}
        </Typography>
        <Box>{pages > 1 && <Pagination page={page} count={pages} shape="rounded" color="primary" onChange={(_, p) => onPage(p)} />}</Box>
      </Stack>
    </>
  );
}
