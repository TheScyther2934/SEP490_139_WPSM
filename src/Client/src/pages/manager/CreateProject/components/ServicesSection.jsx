import { useState } from "react";
import {
  Alert, Button, Card, CardContent, Chip, IconButton, MenuItem, Stack, Table, TableBody, TableCell, TableContainer, TableFooter,
  TableHead, TableRow, TextField, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { contractValue, lineProblem } from "../validation";
import { money, moneyInput } from "../../../../utils/format";

// Services the customer chose at the consultation. Their total is the contract value signed in stage 1.
export default function ServicesSection({ lines, catalog, frames, error, onChange, disabled }) {
  const [pick, setPick] = useState("");
  const available = catalog.filter((c) => c.active && !lines.some((l) => Number(l.catalogItemId) === Number(c.id)));
  const add = () => {
    const item = catalog.find((c) => String(c.id) === String(pick));
    if (!item) return;
    onChange([...lines, { catalogItemId: Number(item.id), name: item.name, category: item.category, unit: item.unit, description: item.name, quantity: 1, unitPrice: item.basePrice }]);
    setPick("");
  };
  const patch = (id, values) => onChange(lines.map((l) => (l.catalogItemId === id ? { ...l, ...values } : l)));
  return (
    <Card>
      <CardContent>
        <Typography variant="h6">3. Dịch vụ khách đặt</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Sảnh, thực đơn, số bàn, chụp ảnh, bê lễ, áo cưới… Tổng các dòng là giá trị hợp đồng lúc ký.
        </Typography>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ mb: 2 }}>
          <TextField select size="small" fullWidth label="Chọn dịch vụ" value={pick} disabled={disabled} onChange={(e) => setPick(e.target.value)}>
            {available.map((c) => <MenuItem key={c.id} value={c.id}>{c.category} · {c.name} · {money(c.basePrice)}/{c.unit}</MenuItem>)}
          </TextField>
          <Button variant="outlined" startIcon={<AddIcon />} disabled={disabled || !pick} onClick={add}>Thêm</Button>
        </Stack>
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow><TableCell>Dịch vụ</TableCell><TableCell>Số lượng</TableCell><TableCell>Đơn giá</TableCell><TableCell align="right">Thành tiền</TableCell><TableCell /></TableRow>
            </TableHead>
            <TableBody>
              {lines.map((l) => {
                const problem = lineProblem(l, frames);
                return (
                  <TableRow key={l.catalogItemId} sx={{ verticalAlign: "top" }}>
                    <TableCell sx={{ minWidth: 160 }}><Typography fontWeight={600}>{l.name}</Typography><Chip size="small" label={l.category} /></TableCell>
                    <TableCell sx={{ width: 120 }}>
                      <TextField size="small" type="number" value={l.quantity} disabled={disabled} helperText={l.unit}
                        slotProps={{ htmlInput: { min: 1, "aria-label": `Số lượng ${l.name}` } }} onChange={(e) => patch(l.catalogItemId, { quantity: e.target.value })} />
                    </TableCell>
                    <TableCell sx={{ width: 170 }}>
                      <TextField size="small" value={l.unitPrice} disabled={disabled} error={!!problem} helperText={problem}
                        slotProps={{ htmlInput: { inputMode: "numeric", "aria-label": `Đơn giá ${l.name}` } }} onChange={(e) => patch(l.catalogItemId, { unitPrice: moneyInput(e.target.value) })} />
                    </TableCell>
                    <TableCell align="right" sx={{ whiteSpace: "nowrap" }}>{money(Number(l.quantity) * Number(l.unitPrice))}</TableCell>
                    <TableCell><IconButton color="error" disabled={disabled} aria-label={`Bỏ ${l.name}`} onClick={() => onChange(lines.filter((x) => x !== l))}><DeleteIcon /></IconButton></TableCell>
                  </TableRow>
                );
              })}
              {lines.length === 0 && <TableRow><TableCell colSpan={5} align="center" sx={{ py: 3 }}>Chưa chọn dịch vụ.</TableCell></TableRow>}
            </TableBody>
            <TableFooter>
              <TableRow><TableCell colSpan={3} align="right"><b>Giá trị hợp đồng</b></TableCell><TableCell align="right"><b>{money(contractValue(lines))}</b></TableCell><TableCell /></TableRow>
            </TableFooter>
          </Table>
        </TableContainer>
        {error && <Alert severity="error" sx={{ mt: 1 }}>{error}</Alert>}
      </CardContent>
    </Card>
  );
}
