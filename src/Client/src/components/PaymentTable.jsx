import { Card, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from "@mui/material";
import StatusChip from "./StatusChip";
import { dateVi, money } from "../utils/format";

const TYPE = { NRD: "Đặt cọc NRD", T30: "Mốc T-30", Final: "Quyết toán" };

// Payment milestones of one project (M-13 for the Manager, C-10 for the Customer). `action(payment)` adds a last column.
export default function PaymentTable({ payments, action }) {
  return (
    <Card>
      <TableContainer>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Đợt</TableCell><TableCell align="right">Số tiền</TableCell><TableCell>Hạn</TableCell>
              <TableCell>Ngày thu</TableCell><TableCell>Trạng thái</TableCell>{action && <TableCell />}
            </TableRow>
          </TableHead>
          <TableBody>
            {payments.length === 0 ? (
              <TableRow><TableCell colSpan={action ? 6 : 5} align="center">Không có dữ liệu.</TableCell></TableRow>
            ) : payments.map((p, i) => (
              <TableRow key={`${p.type}-${i}`}>
                <TableCell>{TYPE[p.type] ?? p.type}</TableCell>
                <TableCell align="right">{money(p.amount)}</TableCell>
                <TableCell>{dateVi(p.dueDate)}</TableCell>
                <TableCell>{dateVi(p.paidDate)}</TableCell>
                <TableCell><StatusChip kind="payment" status={p.status} /></TableCell>
                {action && <TableCell align="right">{action(p)}</TableCell>}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}
