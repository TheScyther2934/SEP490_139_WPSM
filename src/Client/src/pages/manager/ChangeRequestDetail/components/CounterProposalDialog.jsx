import { useEffect, useState } from "react";
import { Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from "@mui/material";
import CrItemsEditor, { linesValid } from "../../../../components/changeRequests/CrItemsEditor";
import { useAsync } from "../../../../hooks/useAsync";
import { changeRequestService } from "../../../../services/operationsService";

// M-08 counter-proposal: new values per item; saving recalculates the cost and clears both approvals.
export default function CounterProposalDialog({ open, projectCode, cr, busy, onClose, onSave }) {
  const specs = useAsync(() => (open ? changeRequestService.specItems(projectCode) : Promise.resolve([])), [open, projectCode]);
  const [lines, setLines] = useState([]);
  const [note, setNote] = useState("");
  const [showErrors, setShowErrors] = useState(false);
  useEffect(() => {
    if (open) {
      setLines(cr.items.map((i) => ({ specItemId: i.specItemId, newQuantity: i.newQuantity, newUnitPrice: i.newUnitPrice })));
      setNote("");
      setShowErrors(false);
    }
  }, [open, cr]);
  const specItems = specs.data ?? [];
  const save = () => {
    setShowErrors(true);
    if (linesValid(lines, specItems) && note.length <= 500) onSave(lines, note.trim());
  };
  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="md">
      <DialogTitle>Đề xuất lại giá trị</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <CrItemsEditor specItems={specItems} lines={lines} onChange={setLines} showErrors={showErrors} />
          <TextField label="Ghi chú cho khách hàng" multiline minRows={2} value={note} onChange={(e) => setNote(e.target.value)}
            error={note.length > 500} helperText={note.length > 500 ? "Vượt quá độ dài tối đa 500 ký tự." : `${note.length}/500`} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy</Button>
        <Button variant="contained" onClick={save} disabled={busy || specs.loading}>Lưu đề xuất</Button>
      </DialogActions>
    </Dialog>
  );
}
