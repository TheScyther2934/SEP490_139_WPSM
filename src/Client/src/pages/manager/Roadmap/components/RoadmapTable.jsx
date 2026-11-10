import { Button, Card, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { dateTimeVi, dateVi, daysUntil } from "../../../../utils/format";

// M-06 rows. "Mark done" exists only for Manager-owned tasks; Customer tasks are read-only here (UC-21).
export default function RoadmapTable({ tasks, busy, onComplete }) {
  return (
    <Card>
      <TableContainer>
        <Table id="roadmapTable">
          <TableHead>
            <TableRow><TableCell>Cột mốc</TableCell><TableCell>Nhiệm vụ</TableCell><TableCell>Phụ trách</TableCell><TableCell>Hạn</TableCell><TableCell>Trạng thái</TableCell><TableCell /></TableRow>
          </TableHead>
          <TableBody>
            {tasks.length === 0 ? (
              <TableRow><TableCell colSpan={6} align="center">Không có dữ liệu.</TableCell></TableRow>
            ) : tasks.map((t) => {
              const status = t.status === "Pending" && daysUntil(t.dueDate) < 0 ? "Overdue" : t.status;
              return (
                <TableRow key={t.taskId} data-task={t.taskId}>
                  <TableCell>{t.milestone}</TableCell>
                  <TableCell>{t.title}</TableCell>
                  <TableCell>{t.assigneeRole === "Customer" ? "Khách hàng" : "Quản lý"}</TableCell>
                  <TableCell>{dateVi(t.dueDate)}</TableCell>
                  <TableCell><StatusChip kind="task" status={status} />{t.completedAt ? ` ${dateTimeVi(t.completedAt)}` : ""}</TableCell>
                  <TableCell align="right">
                    {t.assigneeRole === "Manager" && t.status !== "Done" && <Button size="small" disabled={busy} onClick={() => onComplete(t)}>Đánh dấu xong</Button>}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}
