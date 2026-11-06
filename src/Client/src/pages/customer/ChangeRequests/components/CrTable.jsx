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

// C-05 ticket table: one row per CR with its cost summary and the link to C-07.
export default function CrTable({ items, page, rowsPerPage, onPageChange }) {
  const rows = items.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);
  return (
    <Card>
      <TableContainer>
        <Table id="cr-table" sx={{ tableLayout: "fixed", minWidth: 860 }}>
          <TableHead>
            <TableRow>
              <TableCell sx={{ width: 130 }}>Mã CR</TableCell>
              <TableCell sx={{ width: 150 }}>Phân loại</TableCell>
              <TableCell>Tóm tắt yêu cầu</TableCell>
              <TableCell align="right" sx={{ width: 160 }}>Tác động chi phí</TableCell>
              <TableCell align="center" sx={{ width: 165 }}>Trạng thái</TableCell>
              <TableCell sx={{ width: 110 }}>Ngày tạo</TableCell>
              <TableCell align="right" sx={{ width: 120 }}>Thao tác</TableCell>
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
                <TableCell sx={{ whiteSpace: "nowrap" }}>
                  <Typography fontWeight={600} color="primary">{cr.code}</Typography>
                  <Typography variant="caption" color="text.secondary">{cr.itemCount} hạng mục</Typography>
                </TableCell>
                <TableCell sx={{ whiteSpace: "nowrap" }}><Chip size="small" variant="outlined" label={typeLabel(cr.type)} /></TableCell>
                <TableCell sx={{ width: "32%", minWidth: 260, maxWidth: 420 }}>
                  <Typography variant="body2" sx={summarySx} title={cr.description}>{cr.description}</Typography>
                  {cr.pendingException === "Pending" && (
                    <Chip size="small" color="warning" variant="outlined" label="Đang chờ Admin ngoại lệ" sx={{ mt: 0.5 }} />
                  )}
                </TableCell>
                <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>
                  <Typography fontWeight={600} color={cr.totalImpact > 0 ? "error.main" : cr.totalImpact < 0 ? "success.main" : "text.secondary"}>
                    {signedAmount(cr.totalImpact)}
                  </Typography>
                  {cr.penaltyFee > 0 && <Typography variant="caption" color="warning.main" component="p">Phạt {money(cr.penaltyFee)}</Typography>}
                </TableCell>
                <TableCell align="center"><StatusChip kind="cr" status={cr.status} /></TableCell>
                <TableCell sx={{ whiteSpace: "nowrap" }}>{dayVi(cr.createdAt)}</TableCell>
                <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>
                  <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                    {cr.status === "Draft" && (
                      <Button size="small" component={RouterLink} to={PATHS.customer.editChangeRequest(cr.code)}>Sửa nháp</Button>
                    )}
                    <Button size="small" variant="outlined" component={RouterLink} to={PATHS.customer.changeRequest(cr.code)}>Xem chi tiết</Button>
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
