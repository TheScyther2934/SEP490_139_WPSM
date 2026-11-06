import { Link as RouterLink } from "react-router-dom";
import { Button, Card, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from "@mui/material";
import StatusChip from "../StatusChip";
import { dateVi } from "../../utils/format";

// C-14 / M-18 list. `linkTo(complaint)` builds the detail URL for the current role.
export default function ComplaintTable({ rows, linkTo }) {
  return (
    <Card>
      <TableContainer>
        <Table id="complaintTable">
          <TableHead>
            <TableRow><TableCell>Mã</TableCell><TableCell>BEO</TableCell><TableCell>Nội dung</TableCell><TableCell>Trạng thái</TableCell><TableCell>Ngày gửi</TableCell><TableCell /></TableRow>
          </TableHead>
          <TableBody>
            {rows.map((c) => (
              <TableRow key={c.code} hover data-complaint={c.code}>
                <TableCell>{c.code}</TableCell>
                <TableCell>v{c.beoVersion}</TableCell>
                <TableCell sx={{ maxWidth: 360 }}>{c.description}</TableCell>
                <TableCell><StatusChip kind="complaint" status={c.status} /></TableCell>
                <TableCell>{dateVi(c.createdAt)}</TableCell>
                <TableCell align="right"><Button size="small" component={RouterLink} to={linkTo(c)}>Xem</Button></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}
