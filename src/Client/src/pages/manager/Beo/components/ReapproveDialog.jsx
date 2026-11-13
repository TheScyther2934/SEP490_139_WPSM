import { useEffect, useState } from "react";
import { Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, List, ListItem, ListItemText, Typography } from "@mui/material";
import { money, signedMoney } from "../../../../utils/format";

// M-12 (UC-29): shows the approved changes since the last freeze; a confirm checkbox creates the new Frozen version.
export default function ReapproveDialog({ open, beo, busy, onClose, onConfirm }) {
  const [checked, setChecked] = useState(false);
  useEffect(() => { if (open) setChecked(false); }, [open]);
  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Duyệt lại BEO lỗi thời</DialogTitle>
      <DialogContent>
        <Typography sx={{ mb: 1 }}>Các thay đổi đã duyệt từ lần đóng băng trước:</Typography>
        <List dense>
          {beo.changesSinceFreeze.map((c) => (
            <ListItem key={c.addendumCode} alignItems="flex-start">
              <ListItemText primary={`${c.addendumCode} (${c.crCode}) · ${signedMoney(c.totalVariance)}`}
                secondary={c.items.map((i) => `${i.name}: ${i.oldQuantity} × ${money(i.oldUnitPrice)} → ${i.newQuantity} × ${money(i.newUnitPrice)}`).join("; ")} />
            </ListItem>
          ))}
        </List>
        <FormControlLabel control={<Checkbox checked={checked} onChange={(e) => setChecked(e.target.checked)} />}
          label="Tôi đã xem các thay đổi và đồng ý tạo phiên bản BEO mới. Hành động này không hoàn tác được (MSG18)." />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy</Button>
        <Button variant="contained" onClick={onConfirm} disabled={!checked || busy}>Duyệt lại</Button>
      </DialogActions>
    </Dialog>
  );
}
