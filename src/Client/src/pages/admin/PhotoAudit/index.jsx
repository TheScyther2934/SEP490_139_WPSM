import { useState } from "react";
import { Link as RouterLink, useParams } from "react-router-dom";
import { Box, Button, Card, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import PhotoMeta from "./components/PhotoMeta";
import { useAsync } from "../../../hooks/useAsync";
import { adminService } from "../../../services/adminService";
import { PATHS } from "../../../routes/paths";

// A-14: one proof photo with its deadline, for auditing late uploads.
export default function PhotoAudit() {
  const { photoId } = useParams();
  const { data, loading, error, reload } = useAsync(() => adminService.getPhoto(photoId), [photoId]);
  const [broken, setBroken] = useState(false);
  return (
    <>
      <PageHeader code="A-14" title="Kiểm tra ảnh minh chứng"
        actions={<Button startIcon={<ArrowBackIcon />} component={RouterLink} to={data ? `${PATHS.admin.photoFeed}?projectCode=${encodeURIComponent(data.projectCode)}` : PATHS.admin.photoFeed}>Về luồng ảnh</Button>} />
      <DataState loading={loading} error={error} onRetry={reload}>
        {data && (
          <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { md: "2fr 1fr" } }}>
            <Card sx={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: 320, bgcolor: "#1e1b19" }}>
              {broken ? <Typography color="#fff">Không tải được ảnh.</Typography>
                : <Box component="img" src={data.imageUrl} alt={data.taskName} onError={() => setBroken(true)} sx={{ maxWidth: "100%", maxHeight: "75vh" }} />}
            </Card>
            <PhotoMeta photo={data} />
          </Box>
        )}
      </DataState>
    </>
  );
}
