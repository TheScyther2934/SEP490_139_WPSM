import { Box, Button, Chip, Stack, Typography } from "@mui/material";
import DownloadIcon from "@mui/icons-material/Download";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import { dateTimeVi } from "../../../../utils/format";

export const EVIDENCE_TYPES = { Transfer: "Nội dung chuyển khoản", Invoice: "Hóa đơn", Contract: "Hợp đồng", Other: "Khác" };

// One uploaded evidence: supplier, type, note, who uploaded it and when, download button.
export default function EvidenceItem({ evidence: e, busy, onDownload }) {
  return (
    <Stack direction={{ xs: "column", sm: "row" }} spacing={{ xs: 1, sm: 2 }}
      sx={{ p: 1.5, borderRadius: 1, bgcolor: "action.hover", alignItems: { sm: "center" } }}>
      <DescriptionOutlinedIcon color="action" sx={{ display: { xs: "none", sm: "block" } }} />
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Stack direction="row" sx={{ alignItems: "center", flexWrap: "wrap", gap: 1 }}>
          <Typography fontWeight={600}>{e.supplierName}</Typography>
          <Chip size="small" variant="outlined" label={EVIDENCE_TYPES[e.evidenceType] ?? e.evidenceType} />
        </Stack>
        {e.note && <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>{e.note}</Typography>}
        <Typography variant="caption" color="text.secondary">{e.uploadedBy} · {dateTimeVi(e.uploadedAt)} · {e.fileName}</Typography>
      </Box>
      <Button size="small" startIcon={<DownloadIcon />} disabled={busy} onClick={() => onDownload(e)} sx={{ alignSelf: { xs: "flex-start", sm: "center" }, flexShrink: 0 }}>
        Tải tệp
      </Button>
    </Stack>
  );
}
