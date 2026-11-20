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
          ? `Duyệt lại phiếu điều phối → phiên bản v${nextVersion}`
          : `Chốt phiếu điều phối (v${nextVersion})`}
      </DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Alert severity={isStale ? "warning" : "info"}>
            {isStale
              ? `Phiếu điều phối v${beo.version} cần duyệt lại vì khách vừa xác nhận hợp đồng phát sinh. Khi xác nhận, hệ thống phát hành phiếu v${nextVersion} có thêm các dòng mới, cập nhật 4 phiếu bộ phận và bổ sung nhiệm vụ điều phối cho các dòng đó. Nhiệm vụ và minh chứng đã làm được giữ nguyên.`
              : `Khi chốt phiếu điều phối v${nextVersion}, hệ thống chuyển kế hoạch tới 4 bộ phận (Bếp, Phục vụ, Trang trí, Kỹ thuật và MC) và tạo nhiệm vụ điều phối còn thiếu.`}
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
            ? `Duyệt lại, phát hành v${nextVersion}`
            : `Chốt phiếu điều phối v${nextVersion}`}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
