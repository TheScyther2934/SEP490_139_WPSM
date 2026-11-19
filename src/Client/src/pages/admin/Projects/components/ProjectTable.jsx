import { useNavigate } from "react-router-dom";
import { Box, Card, Chip, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, Tooltip, Typography } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { PATHS } from "../../../../routes/paths";
import { dateVi, money, slotLabel } from "../../../../utils/format";

// One row per project; the whole row opens the detail. Creator and operating Managers are always visible (accountability).
export default function ProjectTable({ result, pageSizes, onPageChange, onPageSizeChange }) {
  const navigate = useNavigate();
  return (
    <Card>
      <TableContainer>
        <Table id="adminProjectsTable" size="small">
          <TableHead>
            <TableRow>
              <TableCell>Sự kiện</TableCell>
              <TableCell>Ngày cưới</TableCell>
              <TableCell align="center">Trạng thái</TableCell>
              <TableCell align="right">Tổng hợp đồng</TableCell>
              <TableCell align="right">Còn phải thu</TableCell>
              <TableCell>Người tạo</TableCell>
              <TableCell>Người điều hành</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {result.items.length === 0 ? (
              <TableRow><TableCell colSpan={7} align="center" sx={{ py: 5 }}>Không có sự kiện nào khớp với bộ lọc.</TableCell></TableRow>
            ) : result.items.map((p) => (
              <TableRow key={p.projectCode} hover sx={{ cursor: "pointer" }} onClick={() => navigate(PATHS.admin.project(p.projectCode))}>
                <TableCell>
                  <Typography fontWeight={600} color="primary">{p.projectCode}</Typography>
                  <Typography variant="body2">{p.coupleName}</Typography>
                  <Typography variant="caption" color="text.secondary">{p.customerEmail}{p.customerPhone ? ` · ${p.customerPhone}` : ""}</Typography>
                </TableCell>
                <TableCell>
                  <Typography variant="body2">{dateVi(p.eventDate)}</Typography>
                  <Typography variant="caption" color="text.secondary">{p.hall} · Ca {slotLabel(p.slot)}</Typography>
                </TableCell>
                <TableCell align="center"><StatusChip kind="project" status={p.status} /></TableCell>
                <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>{money(p.currentTotal)}</TableCell>
                <TableCell align="right">
                  <Typography variant="body2" color={p.outstanding > 0 ? "warning.main" : "success.main"} fontWeight={600} sx={{ whiteSpace: "nowrap" }}>{money(p.outstanding)}</Typography>
                </TableCell>
                <TableCell>{p.createdBy?.name ?? "—"}</TableCell>
                <TableCell>
                  <Stack direction="row" spacing={0.5} useFlexGap flexWrap="wrap">
                    {p.managers.map((m) => (
                      <Tooltip key={m.userId} title={m.email}><Chip size="small" label={m.name} variant="outlined" /></Tooltip>
                    ))}
                  </Stack>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <Box>
        <TablePagination component="div" count={result.total} page={result.page - 1} rowsPerPage={result.pageSize} rowsPerPageOptions={pageSizes}
          onPageChange={(_, next) => onPageChange(next + 1)} onRowsPerPageChange={(e) => onPageSizeChange(Number(e.target.value))}
          labelRowsPerPage="Số dòng" labelDisplayedRows={({ from, to, count }) => `${from}–${to} trên ${count} sự kiện`} />
      </Box>
    </Card>
  );
}
