import { Button, Card, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from "@mui/material";
import { dateVi } from "../../../../utils/format";

export default function ComplaintTable({ complaints, selectedId, onOpen }) {
  return (
    <Card>
      <TableContainer>
        <Table id="a16Table">
          <TableHead>
            <TableRow>
              <TableCell>Mã</TableCell><TableCell>Dự án</TableCell><TableCell>Nội dung</TableCell><TableCell>Chuyển cấp</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {complaints.map((c) => (
              <TableRow key={c.complaintId} selected={c.complaintId === selectedId}>
                <TableCell>{c.code}</TableCell>
                <TableCell>{c.projectCode}</TableCell>
                <TableCell sx={{ maxWidth: 360 }}>{c.description}</TableCell>
                <TableCell>{c.escalatedAt ? dateVi(c.escalatedAt) : "—"}</TableCell>
                <TableCell><Button size="small" onClick={() => onOpen(c)}>Mở</Button></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}
