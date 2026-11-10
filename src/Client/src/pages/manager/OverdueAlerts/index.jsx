import { Link as RouterLink } from "react-router-dom";
import { Button, Card, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from "@mui/material";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import { useAsync, usePolling } from "../../../hooks/useAsync";
import { managerService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi } from "../../../utils/format";

// M-23 (UC-32): overdue day-of tasks of the Manager's projects; each row opens the Day-of Board.
export default function OverdueAlerts() {
  const { data, loading, error, reload } = useAsync(() => managerService.getOverdue());
  usePolling(() => reload({ silent: true }));
  return (
    <>
      <PageHeader code="M-23" title="Cảnh báo quá hạn" subtitle="Tự làm mới mỗi 30 giây." />
      <DataState loading={loading} error={error} onRetry={reload} empty={!data?.length} emptyText="Không có dữ liệu (MSG01).">
        <Card>
          <TableContainer>
            <Table id="overdueTable">
              <TableHead>
                <TableRow><TableCell>Dự án</TableCell><TableCell>Công việc</TableCell><TableCell>Hạn</TableCell><TableCell>Quá hạn</TableCell><TableCell>Trạng thái</TableCell><TableCell /></TableRow>
              </TableHead>
              <TableBody>
                {data?.map((a) => (
                  <TableRow key={a.taskId}>
                    <TableCell>{a.projectCode} · {a.coupleName}</TableCell>
                    <TableCell>{a.taskName}</TableCell>
                    <TableCell>{dateTimeVi(a.deadline)}</TableCell>
                    <TableCell>{a.overdueMinutes} phút</TableCell>
                    <TableCell><StatusChip kind="task" status={a.status} /></TableCell>
                    <TableCell align="right"><Button size="small" component={RouterLink} to={PATHS.manager.dayOf(a.projectCode)}>Mở bảng ngày cưới</Button></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Card>
      </DataState>
    </>
  );
}
