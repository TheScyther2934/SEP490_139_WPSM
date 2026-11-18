import { Link as RouterLink } from "react-router-dom";
import {
  Button,
  Card,
  Chip,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import PhotoLibraryIcon from "@mui/icons-material/PhotoLibrary";
import { PATHS } from "../../../../routes/paths";
import { dateTimeVi, dateVi } from "../../../../utils/format";

export default function OverdueTable({ alerts }) {
  // SDS: Sort by overdue duration descending (longest overdue tasks appear first)
  const sorted = [...alerts].sort((a, b) => (b.overdueMinutes || 0) - (a.overdueMinutes || 0));

  return (
    <Card>
      <TableContainer>
        <Table id="a15Table">
          <TableHead>
            <TableRow>
              <TableCell>Dự án</TableCell>
              <TableCell>Nhiệm vụ</TableCell>
              <TableCell>Sảnh tiệc</TableCell>
              <TableCell>Quản lý phụ trách</TableCell>
              <TableCell>Ngày sự kiện</TableCell>
              <TableCell>Hạn chót</TableCell>
              <TableCell>Quá hạn</TableCell>
              <TableCell align="center">Trạng thái</TableCell>
              <TableCell align="right">Minh chứng (A-13)</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {sorted.map((a) => (
              <TableRow key={a.taskId} hover>
                <TableCell sx={{ fontWeight: "bold" }}>
                  {a.projectCode}
                  {a.customerName && (
                    <Typography variant="caption" display="block" color="text.secondary">
                      {a.customerName}
                    </Typography>
                  )}
                </TableCell>
                <TableCell sx={{ fontWeight: 600 }}>{a.taskName}</TableCell>
                <TableCell>{a.hall || "—"}</TableCell>
                <TableCell>{a.managerName || "—"}</TableCell>
                <TableCell>{a.eventDate ? dateVi(a.eventDate) : "—"}</TableCell>
                <TableCell>{dateTimeVi(a.deadline)}</TableCell>
                <TableCell>
                  <Typography color="error.main" fontWeight="bold">
                    {a.overdueMinutes} phút
                  </Typography>
                </TableCell>
                <TableCell align="center">
                  <Chip label="Quá hạn" color="error" size="small" sx={{ fontWeight: "bold" }} />
                </TableCell>
                <TableCell align="right">
                  <Button
                    size="small"
                    variant="outlined"
                    color="primary"
                    startIcon={<PhotoLibraryIcon />}
                    component={RouterLink}
                    to={`${PATHS.admin.photoFeed}?projectCode=${encodeURIComponent(a.projectCode)}`}
                  >
                    Xem ảnh minh chứng
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}
