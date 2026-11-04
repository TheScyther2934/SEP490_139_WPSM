import { Link as RouterLink } from "react-router-dom";
import { Card, Chip, Link, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from "@mui/material";
import { PATHS } from "../../../../routes/paths";
import { dateTimeVi } from "../../../../utils/format";

export default function OverdueTable({ alerts }) {
  return (
    <Card>
      <TableContainer>
        <Table id="a15Table">
          <TableHead>
            <TableRow>
              <TableCell>Dự án</TableCell><TableCell>Nhiệm vụ</TableCell><TableCell>Hạn</TableCell>
              <TableCell>Quá hạn</TableCell><TableCell>Trạng thái</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {alerts.map((a) => (
              <TableRow key={a.taskId}>
                <TableCell>{a.projectCode}</TableCell>
                <TableCell>{a.taskName}</TableCell>
                <TableCell>{dateTimeVi(a.deadline)}</TableCell>
                <TableCell>{a.overdueMinutes} phút</TableCell>
                <TableCell><Chip label="Quá hạn" color="error" size="small" /></TableCell>
                <TableCell>
                  <Link component={RouterLink} to={`${PATHS.admin.photoFeed}?projectCode=${encodeURIComponent(a.projectCode)}`}>Mở ảnh minh chứng</Link>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}
