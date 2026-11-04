import {
  Card, Chip, IconButton, Table, TableBody, TableCell, TableContainer, TableFooter, TableHead, TableRow, TextField, Typography,
} from "@mui/material";
import DeleteIcon from "@mui/icons-material/Delete";
import { frameFor, lineError } from "../rules";
import { money, moneyInput } from "../../../../utils/format";

// Editable specification rows; when `readOnly` (baseline frozen) every input is disabled (GBR-01).
export default function SpecTable({ rows, priceFrames, catalog, readOnly, onChange, onRemove, total }) {
  return (
    <Card>
      <TableContainer>
        <Table size="small" id="specItemsTable">
          <TableHead>
            <TableRow>
              <TableCell>#</TableCell><TableCell>Dịch vụ / Nhóm</TableCell><TableCell>Diễn giải</TableCell><TableCell>Số lượng</TableCell>
              <TableCell>Đơn giá</TableCell><TableCell align="right">Thành tiền</TableCell><TableCell>Khung giá</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.length === 0 ? (
              <TableRow><TableCell colSpan={8} align="center" sx={{ py: 4 }}>Chưa có hạng mục. Thêm dịch vụ từ Catalog để bắt đầu.</TableCell></TableRow>
            ) : rows.map((row, index) => {
              const frame = frameFor(priceFrames, row.catalogItemId);
              const error = lineError(row, priceFrames, catalog);
              return (
                <TableRow key={row.catalogItemId} data-spec-item-id={row.catalogItemId} sx={{ verticalAlign: "top" }}>
                  <TableCell>{index + 1}</TableCell>
                  <TableCell sx={{ minWidth: 180 }}>
                    <Typography fontWeight={600}>{row.name}</Typography>
                    <Chip size="small" label={row.category} sx={{ mt: 0.5 }} />
                  </TableCell>
                  <TableCell sx={{ minWidth: 200 }}>
                    <TextField size="small" multiline minRows={2} fullWidth value={row.description} disabled={readOnly}
                      slotProps={{ htmlInput: { "data-spec-description": true, "aria-label": `Diễn giải ${row.name}` } }}
                      onChange={(e) => onChange(row.catalogItemId, { description: e.target.value })} />
                  </TableCell>
                  <TableCell sx={{ minWidth: 110 }}>
                    <TextField size="small" type="number" value={row.quantity} disabled={readOnly} helperText={row.unit}
                      slotProps={{ htmlInput: { min: 1, step: 1, "data-spec-quantity": true, "aria-label": `Số lượng ${row.name}` } }}
                      onChange={(e) => onChange(row.catalogItemId, { quantity: e.target.value })} />
                  </TableCell>
                  <TableCell sx={{ minWidth: 140 }}>
                    <TextField size="small" inputMode="numeric" value={row.unitPrice} disabled={readOnly}
                      slotProps={{ htmlInput: { "data-spec-price": true, "aria-label": `Đơn giá ${row.name}` } }}
                      onChange={(e) => onChange(row.catalogItemId, { unitPrice: moneyInput(e.target.value) })} />
                  </TableCell>
                  <TableCell align="right" sx={{ whiteSpace: "nowrap", fontWeight: 600 }}>{money(Number(row.quantity) * Number(row.unitPrice))}</TableCell>
                  <TableCell sx={{ minWidth: 170 }}>
                    <Typography variant="caption" color={error ? "error" : "success.main"} data-price-status>
                      {error ?? `${money(frame.minPrice)} – ${money(frame.maxPrice)}`}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <IconButton aria-label={`Xóa ${row.name}`} color="error" disabled={readOnly} onClick={() => onRemove(row.catalogItemId)}><DeleteIcon /></IconButton>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
          <TableFooter>
            <TableRow>
              <TableCell colSpan={5} align="right"><b>Tổng cộng đặc tả</b></TableCell>
              <TableCell align="right" id="specTableTotal"><b>{money(total)}</b></TableCell>
              <TableCell colSpan={2} />
            </TableRow>
          </TableFooter>
        </Table>
      </TableContainer>
    </Card>
  );
}
