import { Button, Dialog, DialogActions, DialogContent, DialogTitle, Table, TableBody, TableCell, TableHead, TableRow } from "@mui/material";
import DataState from "../../../../components/DataState";
import { useAsync } from "../../../../hooks/useAsync";
import { configurationAdminService } from "../../../../services/operationsService";
import { dateTimeVi, dateVi, money } from "../../../../utils/format";

// A-04 "Review price history" (UC-38): every version with its effective date; past versions stay for old contracts (GBR-14).
export default function PriceHistoryDialog({ frame, onClose }) {
  const { data, loading, error } = useAsync(
    () => (frame ? configurationAdminService.priceFrameHistory(frame.catalogItemId) : Promise.resolve([])), [frame?.catalogItemId]);
  return (
    <Dialog open={!!frame} onClose={onClose} fullWidth maxWidth="md">
      <DialogTitle>Lịch sử khung giá · {frame?.serviceName}</DialogTitle>
      <DialogContent>
        <DataState loading={loading} error={error} empty={!data?.length}>
          <Table size="small" id="priceHistory">
            <TableHead><TableRow><TableCell>Hiệu lực từ</TableCell><TableCell align="right">Giá sàn</TableCell><TableCell align="right">Giá trần</TableCell><TableCell>Người cập nhật</TableCell><TableCell>Lúc</TableCell></TableRow></TableHead>
            <TableBody>
              {data?.map((h) => (
                <TableRow key={h.priceFrameId}>
                  <TableCell>{dateVi(h.effectiveFrom)}</TableCell><TableCell align="right">{money(h.minPrice)}</TableCell>
                  <TableCell align="right">{money(h.maxPrice)}</TableCell><TableCell>{h.updatedBy}</TableCell><TableCell>{dateTimeVi(h.updatedAt)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </DataState>
      </DialogContent>
      <DialogActions><Button onClick={onClose}>Đóng</Button></DialogActions>
    </Dialog>
  );
}
