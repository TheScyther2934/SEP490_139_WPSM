import { useState } from "react";
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormControlLabel,
  FormLabel,
  Radio,
  RadioGroup,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { money, signedMoney } from "../../../../utils/format";

// C-08: Customer Approve / Reject / Request Adjustment dialog (UC-10 | GBR-04, GBR-13 | MSG20)
export default function CustomerCrDecisionDialog({ open, cr, submitting, onClose, onSubmit }) {
  const [action, setAction] = useState("Approve");
  const [note, setNote] = useState("");
  const [validationError, setValidationError] = useState("");

  if (!cr) return null;

  const needsNote = action === "Reject" || action === "RequestAdjustment";

  const handleConfirm = () => {
    if (needsNote && !note.trim()) {
      setValidationError(
        action === "Reject"
          ? "Vui lòng nhập lý do từ chối yêu cầu thay đổi (MSG20)."
          : "Vui lòng nhập nội dung cần điều chỉnh để Quản lý cập nhật phương án (MSG20)."
      );
      return;
    }
    setValidationError("");
    onSubmit(action, note.trim());
  };

  return (
    <Dialog open={open} onClose={submitting ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>C-08 · Xác nhận quyết định đối với {cr.crCode}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2.2}>
          <Alert severity="info">
            Tổng chênh lệch của yêu cầu này là <strong>{signedMoney(cr.totalVariance)}</strong> (trong đó phí điều chỉnh cận ngày:{" "}
            <strong>{money(cr.penaltyFee)}</strong>). Sau khi phê duyệt, tổng giá trị hợp đồng dự kiến là{" "}
            <strong>{money(cr.projectedProjectTotal)}</strong>.
          </Alert>

          <FormControl>
            <FormLabel sx={{ fontWeight: 700, mb: 0.5 }}>Lựa chọn phản hồi của khách hàng</FormLabel>
            <RadioGroup
              value={action}
              onChange={(e) => {
                setAction(e.target.value);
                setValidationError("");
              }}
            >
              <FormControlLabel
                value="Approve"
                control={<Radio />}
                label="Chấp thuận tác động chi phí & đồng ý lập phụ lục hợp đồng"
              />
              <FormControlLabel
                value="RequestAdjustment"
                control={<Radio />}
                label="Yêu cầu điều chỉnh lại phương án / số lượng (tiếp tục thương lượng)"
              />
              <FormControlLabel
                value="Reject"
                control={<Radio />}
                label="Từ chối / hủy yêu cầu thay đổi này"
              />
            </RadioGroup>
          </FormControl>

          <TextField
            label={needsNote ? "Ghi chú phản hồi (Bắt buộc)" : "Ghi chú xác nhận (Tùy chọn)"}
            required={needsNote}
            multiline
            minRows={3}
            value={note}
            onChange={(e) => {
              setNote(e.target.value);
              if (validationError) setValidationError("");
            }}
            error={Boolean(validationError)}
            helperText={
              validationError ||
              "Mọi phản hồi sẽ được ghi nhận vào nhật ký thương lượng bất biến (GBR-13)."
            }
            placeholder="Nhập ý kiến phản hồi, yêu cầu điều chỉnh hoặc xác nhận đồng ý..."
            fullWidth
          />

          <Typography variant="caption" color="text.secondary">
            Quy tắc GBR-04: Yêu cầu thay đổi chỉ chuyển sang trạng thái Đã phê duyệt (Approved) khi có đủ xác nhận của cả Khách hàng và Quản lý dự án.
          </Typography>
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting}>
          Đóng
        </Button>
        <Button
          variant="contained"
          color={action === "Reject" ? "error" : action === "RequestAdjustment" ? "warning" : "primary"}
          onClick={handleConfirm}
          disabled={submitting}
          id="confirmCrDecisionBtn"
        >
          {submitting ? "Đang gửi..." : "Gửi quyết định"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
