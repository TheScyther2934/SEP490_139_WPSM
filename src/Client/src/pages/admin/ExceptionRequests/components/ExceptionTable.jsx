import { Button, Card, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";

export default function ExceptionTable({ requests, selectedId, onOpen }) {
  return (
    <Card>
      <TableContainer>
        <Table id="a18Table">
          <TableHead>
            <TableRow>
              <TableCell>Mã</TableCell><TableCell>Nguồn</TableCell><TableCell>Dự án</TableCell>
              <TableCell>Yêu cầu</TableCell><TableCell>Trạng thái</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {requests.map((x) => (
              <TableRow key={x.id} selected={x.id === selectedId}>
                <TableCell>{x.code}</TableCell>
                <TableCell>{x.source}</TableCell>
                <TableCell>{x.projectCode}</TableCell>
                <TableCell sx={{ maxWidth: 320 }}>{x.request}</TableCell>
                <TableCell><StatusChip kind="exception" status={x.status} /></TableCell>
                <TableCell><Button size="small" onClick={() => onOpen(x)}>{x.status === "Pending" ? "Xem xét" : "Xem"}</Button></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}
