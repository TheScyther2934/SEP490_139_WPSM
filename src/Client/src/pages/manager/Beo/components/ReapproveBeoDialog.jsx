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

// M-12: Re-approve Stale BEO / Freeze BEO confirmation dialog (UC-27, UC-29 | GBR-05 | MSG18/22)
export default function ReapproveBeoDialog({ open, beo, initialNotes, submitting, onClose, onConfirm }) {
  const [notes, setNotes] = useState(initialNotes || "");

  if (!beo) return null;

  const isStale = beo.status === "Stale";
  const nextVersion = beo.status === "Draft" || beo.status === "Not started" ? beo.version : beo.version + 1;

  return (
    <Dialog open={open} onClose={submitting ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        {isStale
          ? `M-12 · Duyệt lại BEO từ trạng thái Stale → Phiên bản v${nextVersion}`
          : `M-10 · Phê duyệt & Đóng băng BEO (Frozen v${nextVersion})`}
      </DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Alert severity={isStale ? "warning" : "info"}>
            {isStale
              ? `BEO v${beo.version} đang ở trạng thái Stale do phát sinh phụ lục hợp đồng mới (GBR-05 / MSG22). Khi xác nhận, hệ thống sẽ phát hành BEO v${nextVersion} ở trạng thái Frozen và cập nhật đồng bộ 4 phiếu bộ phận (Kitchen, Service, Decor, Tech-MC).`
              : `Khi đóng băng BEO v${nextVersion}, kế hoạch vận hành sẽ được khóa chuẩn và tự động sinh 4 phiếu điều phối bộ phận (Kitchen, Service, Decor, Tech-MC).`}
          </Alert>

          <TextField
            label="Ghi chú điều phối vận hành cho các bộ phận"
            multiline
            minRows={3}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="Nhập lưu ý đặc biệt cho Bếp, Phục vụ, Trang trí, Âm thanh - MC..."
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
          color={isStale ? "warning" : "primary"}
          onClick={() => onConfirm(notes)}
          disabled={submitting}
          id="confirmFreezeBeoBtn"
        >
          {submitting
            ? "Đang phê duyệt..."
            : isStale
            ? `Xác nhận duyệt lại BEO v${nextVersion}`
            : `Đóng băng BEO v${nextVersion}`}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
