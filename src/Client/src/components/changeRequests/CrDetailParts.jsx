import { Alert, Card, CardContent, Chip, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import StatusChip from "../StatusChip";
import { crActionLabel, crTypeLabel } from "./crLabels";
import { dateTimeVi, money, signedMoney } from "../../utils/format";

// Parts shared by C-07 and M-08/M-09: header facts, item comparison, append-only history (GBR-13), exceptions.
export function CrSummary({ cr }) {
  return (
    <Card>
      <CardContent>
        <Stack direction="row" spacing={1} sx={{ mb: 1, flexWrap: "wrap", gap: 1, alignItems: "center" }}>
          <StatusChip kind="cr" status={cr.status} />
          <Chip size="small" variant="outlined" label={crTypeLabel(cr.type)} />
          <Chip size="small" variant="outlined" label={`Tạo bởi ${cr.createdByRole === "Customer" ? "khách hàng" : "Manager"}: ${cr.createdByName}`} />
        </Stack>
        <Typography sx={{ whiteSpace: "pre-wrap" }}>{cr.reason}</Typography>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={3} sx={{ mt: 2 }}>
          <Typography variant="body2" color="text.secondary">Khách hàng duyệt: <b data-field="customerApproved">{cr.customerApprovedAt ? dateTimeVi(cr.customerApprovedAt) : "Chưa"}</b></Typography>
          <Typography variant="body2" color="text.secondary">Manager duyệt: <b data-field="managerApproved">{cr.managerApprovedAt ? dateTimeVi(cr.managerApprovedAt) : "Chưa"}</b></Typography>
        </Stack>
        {cr.beoEffect && <Alert severity={cr.beoEffect.includes("Stale") ? "warning" : "info"} sx={{ mt: 2 }}>{cr.beoEffect}</Alert>}
      </CardContent>
    </Card>
  );
}

export function CrItemsTable({ items }) {
  return (
    <Card>
      <TableContainer>
        <Table size="small" id="crItemsTable">
          <TableHead>
            <TableRow>
              <TableCell>Hạng mục</TableCell><TableCell align="right">Giá trị cũ</TableCell><TableCell align="right">Giá trị mới</TableCell>
              <TableCell align="right">Chênh lệch</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.map((i) => (
              <TableRow key={i.specItemId}>
                <TableCell>{i.name}</TableCell>
                <TableCell align="right">{i.oldQuantity} {i.unit} × {money(i.oldUnitPrice)}</TableCell>
                <TableCell align="right">{i.newQuantity} {i.unit} × {money(i.newUnitPrice)}</TableCell>
                <TableCell align="right">{signedMoney(i.delta)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Card>
  );
}

export function CrHistory({ history }) {
  return (
    <Card>
      <CardContent>
        <Typography variant="subtitle1" fontWeight={600} sx={{ mb: 1 }}>Lịch sử thương lượng</Typography>
        <Stack spacing={1.5} id="crHistory">
          {history.map((h, i) => (
            <div key={i}>
              <Typography variant="body2" fontWeight={600}>{crActionLabel(h.action)}</Typography>
              <Typography variant="caption" color="text.secondary">{h.actor} ({h.actorRole}) · {dateTimeVi(h.createdAt)}</Typography>
              {h.note && <Typography variant="body2" sx={{ whiteSpace: "pre-wrap" }}>{h.note}</Typography>}
            </div>
          ))}
        </Stack>
      </CardContent>
    </Card>
  );
}

export function CrExceptions({ exceptions }) {
  if (!exceptions?.length) return null;
  return (
    <Stack spacing={1}>
      {exceptions.map((e) => (
        <Alert key={e.code} severity={e.status === "Approved" ? "success" : e.status === "Rejected" ? "error" : "warning"}>
          Ngoại lệ {e.code}: <StatusChip kind="exception" status={e.status} /> {e.note}{e.decisionNote ? ` · Admin: ${e.decisionNote}` : ""}
        </Alert>
      ))}
    </Stack>
  );
}
