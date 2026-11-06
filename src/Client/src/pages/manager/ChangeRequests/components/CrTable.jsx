import { Link as RouterLink } from "react-router-dom";
import {
  Button, Card, Chip, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, Typography,
} from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { typeLabel, signedAmount } from "../../../../components/changeRequest/crMeta";
import { dayVi, money } from "../../../../utils/format";
import { PATHS } from "../../../../routes/paths";

const summarySx = {
  display: "-webkit-box",
  WebkitLineClamp: 2,
  WebkitBoxOrient: "vertical",
  overflow: "hidden",
};

// M-07 pipeline table. "Người tạo" shows the ticket origin (Customer vs Manager on behalf), M-08 opens on a row.
export default function CrTable({ items, projectCode, page, rowsPerPage, onPageChange }) {
  const rows = items.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);
  return (
    <Card>
      <TableContainer>
        <Table id="cr-table" sx={{ tableLayout: "fixed", minWidth: 900 }}>
          <TableHead>
            <TableRow>
              <TableCell sx={{ width: 120 }}>Mã CR</TableCell>
              <TableCell sx={{ width: 150 }}>Phân loại</TableCell>
              <TableCell>Tóm tắt &amp; hạng mục</TableCell>
              <TableCell sx={{ width: 175 }}>Người tạo</TableCell>
              <TableCell align="right" sx={{ width: 145 }}>Tác động chi phí</TableCell>
              <TableCell align="center" sx={{ width: 185 }}>Trạng thái</TableCell>
              <TableCell align="right" sx={{ width: 105 }}>Thao tác</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 4 }}>
                  Không có yêu cầu thay đổi nào khớp với bộ lọc hiện tại.
                </TableCell>
              </TableRow>
            ) : rows.map((cr) => (
              <TableRow key={cr.code} hover>
                <TableCell sx={{ whiteSpace: "nowrap" }}><Typography fontWeight={600} color="primary">{cr.code}</Typography></TableCell>
                <TableCell sx={{ whiteSpace: "nowrap" }}><Chip size="small" variant="outlined" label={typeLabel(cr.type)} /></TableCell>
                <TableCell sx={{ width: "28%", minWidth: 220 }}>
                  <Typography variant="body2" sx={summarySx} title={cr.description}>{cr.description}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {cr.itemCount} hạng mục · Khách {cr.customerApproved ? "đã duyệt" : "chưa duyệt"} · Quản lý {cr.managerApproved ? "đã duyệt" : "chưa duyệt"}
                  </Typography>
                  {cr.pendingException === "Pending" && (
                    <Chip size="small" color="warning" variant="outlined" label="Chờ Admin ngoại lệ" sx={{ mt: 0.5 }} />
                  )}
                </TableCell>
                <TableCell sx={{ minWidth: 150 }}>
                  <Typography variant="body2" noWrap title={cr.createdByName}>{cr.createdByName}</Typography>
                  <Typography variant="caption" color="text.secondary" noWrap component="p">{cr.createdByRole === "Customer" ? "Khách hàng" : "Quản lý tạo"} · {dayVi(cr.createdAt)}</Typography>
                </TableCell>
                <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>
                  <Typography fontWeight={600} color={cr.totalImpact > 0 ? "error.main" : cr.totalImpact < 0 ? "success.main" : "text.secondary"}>
                    {signedAmount(cr.totalImpact)}
                  </Typography>
                  <Typography variant="caption" color={cr.penaltyFee > 0 ? "warning.main" : "text.secondary"} component="p">
                    {cr.penaltyFee > 0 ? `Phạt ${money(cr.penaltyFee)}` : "Không phí phạt"}
                  </Typography>
                </TableCell>
                <TableCell align="center"><StatusChip kind="cr" status={cr.status} /></TableCell>
                <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>
                  <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                    {cr.status === "Draft" && (
                      <Button size="small" component={RouterLink} to={PATHS.manager.editChangeRequest(projectCode, cr.code)}>Sửa nháp</Button>
                    )}
                    <Button size="small" variant="outlined" component={RouterLink} to={PATHS.manager.changeRequest(projectCode, cr.code)}>Chi tiết</Button>
                  </Stack>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <TablePagination component="div" count={items.length} page={page} rowsPerPage={rowsPerPage} rowsPerPageOptions={[rowsPerPage]}
        onPageChange={(_, next) => onPageChange(next)}
        labelDisplayedRows={({ from, to, count }) => `${from}–${to} trên ${count} yêu cầu`} />
    </Card>
  );
}
