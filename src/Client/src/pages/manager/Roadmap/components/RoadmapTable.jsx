import { Card, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { dateVi } from "../../../../utils/format";

export default function RoadmapTable({ tasks }) {
  return (
    <Card>
      <TableContainer>
        <Table>
          <TableHead>
            <TableRow><TableCell>Cột mốc</TableCell><TableCell>Nhiệm vụ</TableCell><TableCell>Phụ trách</TableCell><TableCell>Hạn</TableCell><TableCell>Trạng thái</TableCell></TableRow>
          </TableHead>
          <TableBody>
            {tasks.length === 0 ? (
              <TableRow><TableCell colSpan={5} align="center">Không có dữ liệu.</TableCell></TableRow>
            ) : tasks.map((t, i) => (
              <TableRow key={`${t.milestone}-${t.title}-${i}`}>
                <TableCell>{t.milestone}</TableCell>
                <TableCell>{t.title}</TableCell>
                <TableCell>{t.assigneeRole === "Customer" ? "Khách hàng" : "Quản lý"}</TableCell>
                <TableCell>{dateVi(t.dueDate)}</TableCell>
                <TableCell><StatusChip kind="task" status={t.status} /></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}
