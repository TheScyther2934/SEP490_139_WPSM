import { useState } from "react";
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
} from "@mui/material";

// M-20: Escalate Complaint to Admin Dialog (UC-36 | BF-08)
export default function EscalateComplaintDialog({ open, complaint, submitting, onClose, onConfirm }) {
  const [note, setNote] = useState("");
  const [validationError, setValidationError] = useState("");

  if (!complaint) return null;

  const handleSubmit = () => {
    const trimmed = note.trim();
    if (!trimmed) {
      setValidationError("Vui lòng nhập lý do chuyển cấp lên Admin và đề xuất sơ bộ của Manager.");
      return;
    }
    setValidationError("");
    onConfirm(trimmed);
  };

  return (
    <Dialog open={open} onClose={submitting ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>M-20 · Chuyển cấp Khiếu nại lên Admin ({complaint.complaintCode})</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Alert severity="warning">
            Khi chuyển cấp (Escalate), hồ sơ khiếu nại <strong>{complaint.complaintCode}</strong> sẽ được đẩy tới màn hình{" "}
            <strong>A-16 / A-17</strong> của Ban Quản trị (Admin) để ra quyết định bồi hoàn hoặc xử lý cuối cùng.
          </Alert>

          <TextField
            label="Ghi chú chuyển cấp & Tóm tắt hiện trường (Bắt buộc · tối đa 500 ký tự) *"
            multiline
            minRows={4}
            value={note}
            onChange={(e) => {
              setNote(e.target.value);
              if (validationError) setValidationError("");
            }}
            error={Boolean(validationError)}
            helperText={validationError || `${note.length}/500 ký tự`}
            inputProps={{ maxLength: 500 }}
            placeholder="Nêu rõ nguyên nhân sai lệch, kết quả kiểm tra ảnh nghiệm thu và lý do vượt thẩm quyền giải quyết của Manager..."
            fullWidth
          />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting}>
          Hủy
        </Button>
        <Button
          variant="contained"
          color="error"
          onClick={handleSubmit}
          disabled={submitting}
          id="confirmEscalateComplaintBtn"
        >
          {submitting ? "Đang chuyển cấp..." : "Xác nhận Chuyển lên Admin"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
