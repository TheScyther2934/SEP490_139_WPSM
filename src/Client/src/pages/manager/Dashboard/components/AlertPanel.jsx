import { Link as RouterLink } from "react-router-dom";
import { Alert, Button, Card, CardContent, Stack, Typography } from "@mui/material";
import { PATHS } from "../../../../routes/paths";

// Red panel: Stale BEOs need re-approval. Overdue day-of tasks join this panel when M-23 has its API.
export default function AlertPanel({ stale }) {
  return (
    <Card sx={{ height: "100%" }}>
      <CardContent>
        <Typography variant="h6" gutterBottom>Cần xử lý ngay</Typography>
        {stale.length === 0 ? <Typography color="text.secondary">Không có cảnh báo.</Typography> : (
          <Stack spacing={1}>
            {stale.map((p) => (
              <Alert key={p.id} severity="error" action={<Button color="inherit" size="small" component={RouterLink} to={PATHS.manager.beo(p.id)}>Mở BEO</Button>}>
                {p.id} · BEO lỗi thời, cần duyệt lại
              </Alert>
            ))}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}
