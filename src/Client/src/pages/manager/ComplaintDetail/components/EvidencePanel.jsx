import { Card, CardContent, Stack, Typography } from "@mui/material";
import FileButton from "../../../../components/FileButton";
import PhotoCard from "../../../../components/PhotoCard";
import { DEPARTMENTS } from "../../Beo/departments";
import { timeVi } from "../../../../utils/format";

// M-19: the disputed BEO version's department sheets and the event-day photos, for side-by-side review.
export default function EvidencePanel({ complaint }) {
  return (
    <Card>
      <CardContent>
        <Typography variant="subtitle1" fontWeight={600}>BEO v{complaint.beoVersion}</Typography>
        <Stack direction="row" sx={{ my: 1, flexWrap: "wrap", gap: 1 }}>
          {complaint.beoSheets.length === 0 ? <Typography color="text.secondary">Không có file PDF.</Typography>
            : complaint.beoSheets.map((s) => <FileButton key={s.department} fileId={s.fileId}>{DEPARTMENTS[s.department]}</FileButton>)}
        </Stack>
        <Typography variant="subtitle1" fontWeight={600} sx={{ mt: 2 }}>Ảnh minh chứng ngày cưới</Typography>
        <Stack direction="row" sx={{ mt: 1, flexWrap: "wrap", gap: 1 }}>
          {complaint.photos.length === 0 ? <Typography color="text.secondary">Không có ảnh.</Typography> : complaint.photos.map((p) => (
            <div key={p.photoId} style={{ width: 150 }}><PhotoCard photo={p} height={100} title={`Ảnh #${p.photoId}`} subtitle={timeVi(p.uploadedAt)} /></div>
          ))}
        </Stack>
      </CardContent>
    </Card>
  );
}
