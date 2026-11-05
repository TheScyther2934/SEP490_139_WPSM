import { Link as RouterLink } from "react-router-dom";
import { Box, Button, Card, CardContent, Typography } from "@mui/material";
import PhotoCard from "../../../../components/PhotoCard";
import { PATHS } from "../../../../routes/paths";
import { dateVi } from "../../../../utils/format";

export default function LatestPhoto({ photo }) {
  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>Ảnh thực tế mới nhất</Typography>
        {photo ? (
          <Box sx={{ maxWidth: 360 }}>
            <PhotoCard photo={photo} height={200} title="Ảnh chuẩn bị hôn lễ" subtitle={`${photo.uploaderName} · ${dateVi(photo.uploadedAt)}`} />
          </Box>
        ) : (
          <Typography color="text.secondary">Ảnh chuẩn bị sẽ xuất hiện tại đây khi Ban điều phối tải lên hình ảnh thực tế.</Typography>
        )}
        <Button component={RouterLink} to={PATHS.customer.livePhotos} sx={{ mt: 1 }}>Xem tất cả ảnh</Button>
      </CardContent>
    </Card>
  );
}
