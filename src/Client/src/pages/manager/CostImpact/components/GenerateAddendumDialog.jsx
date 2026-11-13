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
  Typography,
} from "@mui/material";
import UploadFileOutlinedIcon from "@mui/icons-material/UploadFileOutlined";
import { money, signedMoney } from "../../../../utils/format";

// M-09: Single-transaction Addendum Generation & Optional Signed PDF Upload Dialog
export default function GenerateAddendumDialog({ open, cr, submitting, onClose, onConfirm }) {
  const [note, setNote] = useState("");
  const [selectedFile, setSelectedFile] = useState(null);

  if (!cr) return null;

  const handleSubmit = () => {
    onConfirm({ note: note.trim(), file: selectedFile });
  };

  return (
    <Dialog open={open} onClose={submitting ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>M-09 · Phê duyệt tác động chi phí & Phát hành Phụ lục ({cr.crCode})</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Alert severity="info">
            Hệ thống sẽ thực hiện trong <strong>một giao dịch nguyên tử (Single Transaction)</strong>:
            <ul style={{ margin: "6px 0 0 18px", padding: 0 }}>
              <li>Chốt trạng thái CR <strong>{cr.crCode}</strong> sang <strong>Approved</strong></li>
              <li>Phát hành Phụ lục hợp đồng với tổng chênh lệch <strong>{signedMoney(cr.totalVariance)}</strong></li>
              <li>Cập nhật tổng giá trị hợp đồng mới: <strong>{money(cr.projectedProjectTotal)}</strong></li>
              <li>Chuyển mọi BEO đang Frozen sang trạng thái <strong>Stale</strong> để rà soát lại (GBR-05)</li>
            </ul>
          </Alert>

          {cr.impactsBeo && (
            <Alert severity="warning">
              Dự án hiện có BEO ở trạng thái <strong>{cr.beoStatus}</strong>. Ngay khi phát hành phụ lục này, BEO sẽ được đánh dấu{" "}
              <strong>Stale</strong> để Quản lý duyệt lại ở màn hình M-12.
            </Alert>
          )}

          <TextField
            label="Ghi chú phê duyệt & phát hành phụ lục"
            multiline
            minRows={2}
            value={note}
            onChange={(e) => setNote(e.target.value)}
            placeholder="Ví dụ: Đã đối chiếu năng lực cung ứng và thống nhất phụ lục với khách hàng..."
            fullWidth
          />

          <Stack spacing={1}>
            <Typography variant="body2" fontWeight={700}>
              Đính kèm bản scan Phụ lục đã ký (Tùy chọn · PDF/JPG/PNG ≤ 10MB)
            </Typography>
            <Button variant="outlined" component="label" startIcon={<UploadFileOutlinedIcon />} sx={{ alignSelf: "flex-start" }}>
              {selectedFile ? selectedFile.name : "Chọn file phụ lục đã ký..."}
              <input
                type="file"
                hidden
                accept=".pdf,.jpg,.jpeg,.png"
                onChange={(e) => setSelectedFile(e.target.files?.[0] || null)}
              />
            </Button>
            <Typography variant="caption" color="text.secondary">
              Nếu chưa có bản chữ ký ngay, phụ lục sẽ được tạo ở trạng thái <strong>Generated</strong> và có thể tải bản ký lên sau.
            </Typography>
          </Stack>
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting}>
          Hủy
        </Button>
        <Button variant="contained" onClick={handleSubmit} disabled={submitting} id="confirmGenerateAddendumBtn">
          {submitting ? "Đang xử lý giao dịch..." : "Xác nhận & Phát hành Phụ lục"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
