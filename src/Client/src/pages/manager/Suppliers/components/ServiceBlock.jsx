import { Box, Button, Card, CardContent, Chip, Stack, Typography } from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ReportProblemIcon from "@mui/icons-material/ReportProblem";
import FolderOpenIcon from "@mui/icons-material/FolderOpen";
import AddIcon from "@mui/icons-material/Add";
import EvidenceItem from "./EvidenceItem";

// One service that needs an outside supplier, with its evidence right below it.
// `line` is null for the group of evidence not tied to a service. Missing evidence: orange border, red once overdue.
export default function ServiceBlock({ line, evidences, overdue, canUpload, busy, onUpload, onDownload }) {
  const other = !line;
  const missing = !other && evidences.length === 0;
  const tone = missing ? (overdue ? "error" : "warning") : other ? "grey" : "success";
  return (
    <Card variant="outlined" sx={{ borderLeft: 4, borderLeftColor: tone === "grey" ? "grey.400" : `${tone}.main` }}>
      <CardContent sx={{ "&:last-child": { pb: 2 } }}>
        <Stack direction="row" spacing={1.5} sx={{ alignItems: "center" }}>
          {other ? <FolderOpenIcon color="action" /> : missing ? <ReportProblemIcon color={tone} /> : <CheckCircleIcon color="success" />}
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography fontWeight={600}>{other ? "Minh chứng khác" : line.serviceName}</Typography>
            <Typography variant="body2" color="text.secondary">
              {other ? "Không gắn với dịch vụ cụ thể" : `${line.category} · ${evidences.length ? `${evidences.length} minh chứng` : "chưa có minh chứng"}`}
            </Typography>
          </Box>
          {missing && <Chip size="small" color={tone} label={overdue ? "Quá hạn" : "Cần tải"} sx={{ display: { xs: "none", sm: "inline-flex" } }} />}
          {canUpload && !other && (
            <Button size="small" variant={missing ? "contained" : "outlined"} color={missing ? tone : "primary"} startIcon={<AddIcon />}
              onClick={() => onUpload(line.specItemId)} sx={{ flexShrink: 0 }}>
              Tải lên
            </Button>
          )}
        </Stack>
        {evidences.length > 0 && (
          <Stack spacing={1} sx={{ mt: 1.5, pl: { sm: 4.5 } }}>
            {evidences.map((e) => <EvidenceItem key={e.id} evidence={e} busy={busy} onDownload={onDownload} />)}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}
