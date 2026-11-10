import { Link as RouterLink } from "react-router-dom";
import { Button, Card, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from "@mui/material";
import StatusChip from "../StatusChip";
import { crTypeLabel } from "./crLabels";
import { dateVi, signedMoney } from "../../utils/format";

// C-05 / M-07 table. `linkTo(cr)` builds the detail URL for the current role.
export default function CrTable({ rows, linkTo }) {
  return (
    <Card>
      <TableContainer>
        <Table id="crTable">
          <TableHead>
            <TableRow>
              <TableCell>Mã CR</TableCell><TableCell>Loại</TableCell><TableCell>Nội dung</TableCell><TableCell>Người tạo</TableCell>
              <TableCell align="right">Ảnh hưởng chi phí</TableCell><TableCell>Trạng thái</TableCell><TableCell>Ngày tạo</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((cr) => (
              <TableRow key={cr.code} hover data-cr={cr.code}>
                <TableCell>{cr.code}</TableCell>
                <TableCell>{crTypeLabel(cr.type)}</TableCell>
                <TableCell sx={{ maxWidth: 280 }}>{cr.reason}</TableCell>
                <TableCell>{cr.createdByRole === "Customer" ? "Khách hàng" : "Manager"}</TableCell>
                <TableCell align="right">{signedMoney(cr.totalImpact)}</TableCell>
                <TableCell><StatusChip kind="cr" status={cr.status} /></TableCell>
                <TableCell>{dateVi(cr.createdAt)}</TableCell>
                <TableCell align="right"><Button size="small" component={RouterLink} to={linkTo(cr)}>Xem</Button></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}
