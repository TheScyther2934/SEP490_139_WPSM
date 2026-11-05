import { Link as RouterLink } from "react-router-dom";
import { Button, Card, Chip, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, Typography } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { PATHS } from "../../../../routes/paths";
import { countdownLabel, dateVi, daysUntil, slotLabel } from "../../../../utils/format";

export default function ProjectTable({ projects, page, rowsPerPage, onPageChange }) {
  const rows = projects.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);
  return (
    <Card>
      <TableContainer>
        <Table id="projects-table">
          <TableHead>
            <TableRow>
              <TableCell>Mã dự án</TableCell><TableCell>Khách hàng</TableCell><TableCell>Sảnh</TableCell><TableCell align="center">Ca</TableCell>
              <TableCell>Ngày tổ chức</TableCell><TableCell align="center">Đếm ngược</TableCell><TableCell align="center">Trạng thái</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.length === 0 ? (
              <TableRow><TableCell colSpan={8} align="center" sx={{ py: 4 }}>Không có dự án nào khớp với bộ lọc hiện tại.</TableCell></TableRow>
            ) : rows.map((p) => (
              <TableRow key={p.id} hover>
                <TableCell><Typography fontWeight={600} color="primary">{p.id}</Typography></TableCell>
                <TableCell>{p.coupleName}</TableCell>
                <TableCell>{p.hall}</TableCell>
                <TableCell align="center">{slotLabel(p.slot)}</TableCell>
                <TableCell>{dateVi(p.eventDate)}</TableCell>
                <TableCell align="center">
                  <Chip size="small" label={countdownLabel(p.eventDate)} color={daysUntil(p.eventDate) === 0 ? "warning" : "default"} variant="outlined" />
                </TableCell>
                <TableCell align="center"><StatusChip kind="project" status={p.status} /></TableCell>
                <TableCell align="right"><Button size="small" variant="outlined" component={RouterLink} to={PATHS.manager.project(p.id)}>Mở</Button></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <TablePagination component="div" count={projects.length} page={page} rowsPerPage={rowsPerPage} rowsPerPageOptions={[rowsPerPage]}
        onPageChange={(_, next) => onPageChange(next)} labelDisplayedRows={({ from, to, count }) => `${from}–${to} trên ${count} dự án`} />
    </Card>
  );
}
