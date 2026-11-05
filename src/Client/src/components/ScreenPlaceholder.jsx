import { Card, CardContent, Typography } from "@mui/material";
import ConstructionIcon from "@mui/icons-material/Construction";
import PageHeader from "./PageHeader";

// Route exists so links and role checks work; the screen waits for its API.
export default function ScreenPlaceholder({ code, title, description }) {
  return (
    <>
      <PageHeader code={code} title={title} />
      <Card>
        <CardContent sx={{ textAlign: "center", py: 6 }}>
          <ConstructionIcon color="secondary" sx={{ fontSize: 48 }} />
          <Typography variant="h6" sx={{ mt: 1 }}>Màn hình đang được triển khai</Typography>
          <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 560, mx: "auto" }}>
            {description ?? `Màn ${code} chưa có API. Đường dẫn và phân quyền đã sẵn sàng.`}
          </Typography>
        </CardContent>
      </Card>
    </>
  );
}
