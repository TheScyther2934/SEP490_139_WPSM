import { Card, CardContent, Divider, Stack, Table, TableBody, TableCell, TableRow, Typography } from "@mui/material";
import StatusChip from "./StatusChip";
import { dateVi, money } from "../utils/format";

// Final invoice of an event: contract after adjustments + confirmed addon contracts - deposit, paid once.
export default function InvoiceCard({ invoice }) {
  if (!invoice) return null;
  return (
    <Card>
      <CardContent>
        <Stack direction="row" spacing={1} sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
          <Typography variant="h6">Hóa đơn tất toán {invoice.code}</Typography>
          <StatusChip kind="invoice" status={invoice.status} />
        </Stack>
        <Table size="small">
          <TableBody>
            {invoice.lines.map((l, i) => (
              <TableRow key={i}>
                <TableCell sx={{ pl: 0 }}>{l.description}</TableCell>
                <TableCell align="right" sx={{ pr: 0, whiteSpace: "nowrap", color: l.amount < 0 ? "success.main" : undefined }}>{money(l.amount)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        <Divider sx={{ my: 1 }} />
        <Stack direction="row" sx={{ justifyContent: "space-between" }}>
          <Typography fontWeight={700}>{invoice.status === "Paid" ? "Đã thanh toán" : "Cần thanh toán"}</Typography>
          <Typography fontWeight={700} color="primary">{money(invoice.amountDue)}</Typography>
        </Stack>
        <Typography variant="caption" color="text.secondary">
          Phát hành {dateVi(invoice.issuedAt)} · Hạn thanh toán {dateVi(invoice.dueDate)}{invoice.paidAt ? ` · Thanh toán ${dateVi(invoice.paidAt)}` : ""}
        </Typography>
      </CardContent>
    </Card>
  );
}
