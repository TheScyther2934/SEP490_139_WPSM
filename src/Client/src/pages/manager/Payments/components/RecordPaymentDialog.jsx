import { useState, useEffect } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormHelperText,
  InputAdornment,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import CloudUploadIcon from "@mui/icons-material/CloudUpload";
import AttachFileIcon from "@mui/icons-material/AttachFile";
import { projectService } from "../../../../services/projectService";
import { money, isoDate, errorMessage } from "../../../../utils/format";

export default function RecordPaymentDialog({ open, onClose, projectCode, summary, onSuccess }) {
  const [paymentType, setPaymentType] = useState("T30");
  const [amount, setAmount] = useState("");
  const [paymentDate, setPaymentDate] = useState(isoDate(new Date()));
  const [method, setMethod] = useState("Bank");
  const [receiptFile, setReceiptFile] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  const todayStr = isoDate(new Date());
  const outstandingBalance = summary?.outstandingBalance ?? 0;
  const isEventCompleted = summary?.isEventCompleted ?? false;

  useEffect(() => {
    if (open) {
      setError(null);
      setPaymentType("T30");
      setAmount("");
      setPaymentDate(isoDate(new Date()));
      setMethod("Bank");
      setReceiptFile(null);
    }
  }, [open]);

  const handleFillMax = () => {
    setAmount(String(outstandingBalance));
  };

  const handleFileChange = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > 10 * 1024 * 1024) {
      setError("File chứng từ vượt quá giới hạn 10 MB.");
      return;
    }

    const ext = file.name.split(".").pop()?.toLowerCase();
    if (!["jpg", "jpeg", "png", "pdf"].includes(ext)) {
      setError("Định dạng file không hợp lệ. Chỉ chấp nhận JPG, PNG hoặc PDF.");
      return;
    }

    setError(null);
    setReceiptFile(file);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);

    const numAmount = Number(amount);
    if (!numAmount || numAmount <= 0) {
      setError("Vui lòng nhập số tiền thanh toán lớn hơn 0.");
      return;
    }

    if (numAmount > outstandingBalance) {
      setError(`Số tiền thanh toán không được vượt quá số dư còn lại (${money(outstandingBalance)}). (MSG11)`);
      return;
    }

    if (paymentDate > todayStr) {
      setError("Ngày thanh toán không được là ngày trong tương lai.");
      return;
    }

    if (paymentType === "FINAL" && !isEventCompleted) {
      setError("Chỉ có thể ghi nhận Final Payment sau khi sự kiện cưới đã diễn ra hoàn tất.");
      return;
    }

    if (method === "Bank" && !receiptFile) {
      setError("Phương thức chuyển khoản bắt buộc phải tải lên chứng từ / ủy nhiệm chi.");
      return;
    }

    try {
      setSubmitting(true);
      const formData = new FormData();
      formData.append("paymentType", paymentType);
      formData.append("amount", String(numAmount));
      formData.append("paymentDate", paymentDate);
      formData.append("method", method);
      if (receiptFile) {
        formData.append("receipt", receiptFile);
      }

      const res = await projectService.recordPayment(projectCode, formData);
      onSuccess(res);
      onClose();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={submitting ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ fontWeight: 600 }}>Ghi nhận thanh toán (M-14)</DialogTitle>
      <Box component="form" onSubmit={handleSubmit}>
        <DialogContent dividers>
          <Stack spacing={2.5}>
            {error && <Alert severity="error">{error}</Alert>}

            <Alert severity="info" variant="outlined">
              <Typography variant="body2">
                Dư nợ hiện tại: <strong>{money(outstandingBalance)}</strong>
              </Typography>
            </Alert>

            <FormControl fullWidth required>
              <InputLabel id="payment-type-label">Đợt thanh toán</InputLabel>
              <Select
                labelId="payment-type-label"
                value={paymentType}
                label="Đợt thanh toán"
                onChange={(e) => {
                  setPaymentType(e.target.value);
                  setError(null);
                }}
              >
                <MenuItem value="T30">Thanh toán đợt hai (T-30)</MenuItem>
                <MenuItem value="FINAL">Quyết toán hợp đồng (Final Settlement)</MenuItem>
              </Select>
              <FormHelperText>
                {paymentType === "FINAL" && !isEventCompleted
                  ? "Cảnh báo: Tiệc cưới chưa hoàn tất. Không thể thu Final Payment trước ngày cưới."
                  : "Không áp dụng cho NRD (NRD được thu khi khởi tạo dự án)."}
              </FormHelperText>
            </FormControl>

            <Box>
              <TextField
                fullWidth
                required
                type="number"
                label="Số tiền thanh toán (VND)"
                value={amount}
                onChange={(e) => {
                  setAmount(e.target.value);
                  setError(null);
                }}
                inputProps={{ min: 1000, max: outstandingBalance, step: 1000 }}
                InputProps={{
                  endAdornment: <InputAdornment position="end">VND</InputAdornment>,
                }}
                helperText={amount ? `Bằng chữ/số: ${money(amount)}` : undefined}
              />
              {outstandingBalance > 0 && (
                <Button size="small" onClick={handleFillMax} sx={{ mt: 0.5 }}>
                  Điền số dư còn lại ({money(outstandingBalance)})
                </Button>
              )}
            </Box>

            <TextField
              fullWidth
              required
              type="date"
              label="Ngày thanh toán"
              value={paymentDate}
              onChange={(e) => setPaymentDate(e.target.value)}
              inputProps={{ max: todayStr }}
              InputLabelProps={{ shrink: true }}
              helperText="Không được là ngày trong tương lai (UTC+7)."
            />

            <FormControl fullWidth required>
              <InputLabel id="payment-method-label">Phương thức thanh toán</InputLabel>
              <Select
                labelId="payment-method-label"
                value={method}
                label="Phương thức thanh toán"
                onChange={(e) => {
                  setMethod(e.target.value);
                  setError(null);
                }}
              >
                <MenuItem value="Bank">Chuyển khoản ngân hàng (Bank Transfer)</MenuItem>
                <MenuItem value="Cash">Tiền mặt (Cash)</MenuItem>
              </Select>
              <FormHelperText>
                {method === "Bank"
                  ? "Chuyển khoản: Bắt buộc đính kèm ủy nhiệm chi / chứng từ."
                  : "Tiền mặt: Tùy chọn chứng từ đính kèm."}
              </FormHelperText>
            </FormControl>

            <Box>
              <Typography variant="body2" sx={{ mb: 1, fontWeight: 500 }}>
                Chứng từ / Biên nhận {method === "Bank" ? "(Bắt buộc)" : "(Tùy chọn)"}
              </Typography>
              <Stack direction="row" spacing={1} alignItems="center">
                <Button
                  component="label"
                  variant="outlined"
                  startIcon={<CloudUploadIcon />}
                  disabled={submitting}
                >
                  Chọn file chứng từ
                  <input
                    type="file"
                    hidden
                    accept=".pdf,.jpg,.jpeg,.png"
                    onChange={handleFileChange}
                  />
                </Button>
                {receiptFile && (
                  <Chip
                    icon={<AttachFileIcon />}
                    label={receiptFile.name}
                    onDelete={() => setReceiptFile(null)}
                    color="primary"
                    variant="outlined"
                  />
                )}
              </Stack>
              <Typography variant="caption" color="text.secondary" component="p" sx={{ mt: 0.5 }}>
                Định dạng hỗ trợ: JPG, PNG, PDF. Tối đa 10 MB.
              </Typography>
            </Box>
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={onClose} disabled={submitting} color="inherit">
            Hủy
          </Button>
          <Button
            type="submit"
            variant="contained"
            color="primary"
            disabled={submitting || (paymentType === "FINAL" && !isEventCompleted)}
            startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : null}
          >
            {submitting ? "Đang xử lý..." : "Xác nhận ghi nhận"}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  );
}
