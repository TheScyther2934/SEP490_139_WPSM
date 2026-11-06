import { useEffect, useState } from "react";
import { Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, TextField } from "@mui/material";

// Confirm dialog with an optional text field (reject reason, escalation note, exception note...).
// `required` blocks an empty text (MSG02); `maxLength` shows the limit (MSG16).
export default function TextDialog({ open, title, description, label, required, maxLength = 500, confirmText = "Xác nhận",
  color = "primary", busy, onClose, onConfirm, children }) {
  const [text, setText] = useState("");
  const [error, setError] = useState("");
  useEffect(() => { if (open) { setText(""); setError(""); } }, [open]);
  const submit = () => {
    const value = text.trim();
    if (label && required && !value) return setError(`Trường ${label} là bắt buộc.`);
    if (value.length > maxLength) return setError(`Vượt quá độ dài tối đa ${maxLength} ký tự.`);
    onConfirm(value);
  };
  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        {description && <DialogContentText sx={{ mb: 2 }}>{description}</DialogContentText>}
        {children}
        {label && (
          <TextField autoFocus fullWidth multiline minRows={3} label={label} value={text} required={required} sx={{ mt: 1 }}
            onChange={(e) => { setText(e.target.value); setError(""); }} error={!!error}
            helperText={error || `${text.length}/${maxLength}`} slotProps={{ htmlInput: { "data-field": "dialogText" } }} />
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy</Button>
        <Button variant="contained" color={color} onClick={submit} disabled={busy} data-action="confirm">{confirmText}</Button>
      </DialogActions>
    </Dialog>
  );
}
