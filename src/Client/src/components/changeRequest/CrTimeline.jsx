import { Box, Card, CardContent, Chip, Stack, Typography } from "@mui/material";
import { dateTimeVi } from "../../utils/format";
import { historyLabel } from "./crMeta";

const roleLabel = (role) => ({ Customer: "Khách hàng", Manager: "Quản lý", Admin: "Quản trị viên" }[role] ?? role);

// CR History is append-only (GBR-13): newest entry last, exactly as the API stored it.
export default function CrTimeline({ history }) {
  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>Lịch sử thương lượng</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Nhật ký chỉ được ghi thêm, dùng để đối soát khi có tranh chấp.
        </Typography>
        <Stack spacing={2} sx={{ position: "relative", pl: 1 }}>
          {history.length === 0 && <Typography color="text.secondary">Chưa có ghi nhận nào.</Typography>}
          {history.map((entry, index) => (
            <Box key={`${entry.at}-${index}`} sx={{ display: "flex", gap: 1.5 }}>
              <Stack alignItems="center" sx={{ pt: 0.5 }}>
                <Box sx={{ width: 10, height: 10, borderRadius: "50%", bgcolor: "primary.main", flexShrink: 0 }} />
                {index < history.length - 1 && <Box sx={{ width: 2, flex: 1, bgcolor: "divider", mt: 0.5 }} />}
              </Stack>
              <Box sx={{ pb: 1 }}>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
                  <Typography variant="subtitle2">{historyLabel(entry.action)}</Typography>
                  <Chip size="small" variant="outlined" label={roleLabel(entry.actorRole)} />
                </Stack>
                <Typography variant="caption" color="text.secondary">
                  {entry.actorName} · {dateTimeVi(entry.at)}
                </Typography>
                {entry.note && <Typography variant="body2" sx={{ mt: 0.5, whiteSpace: "pre-wrap" }}>{entry.note}</Typography>}
              </Box>
            </Box>
          ))}
        </Stack>
      </CardContent>
    </Card>
  );
}
